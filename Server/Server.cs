using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Shared;
using Shared.Packet;
using Shared.Packet.Packets;

namespace Server;

public class Server {
    public readonly List<Client> Clients = new List<Client>();
    public IEnumerable<Client> ClientsConnected => Clients.Where(client => client.Metadata.ContainsKey("lastGamePacket") && client.Connected);
    public readonly Logger Logger = new Logger("Server");
    private readonly MemoryPool<byte> memoryPool = MemoryPool<byte>.Shared;
    public Func<Client, IPacket, bool>? PacketHandler = null!;
    public event Action<Client, ConnectPacket> ClientJoined = null!;

    public async Task Listen(CancellationToken? token = null) {
        Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        serverSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        serverSocket.Bind(new IPEndPoint(IPAddress.Parse(Settings.Instance.Server.Address), Settings.Instance.Server.Port));
        serverSocket.Listen();

        Logger.Info($"Listening on {serverSocket.LocalEndPoint}");

        try {
            while (true) {
                Socket socket = token.HasValue ? await serverSocket.AcceptAsync(token.Value) : await serverSocket.AcceptAsync();
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);

                if (! Settings.Instance.JsonApi.Enabled) {
                    Logger.Warn($"Accepted connection for client {socket.RemoteEndPoint}");
                }

                // start sub thread to handle client
                try {
#pragma warning disable CS4014
                    Task.Run(() => HandleSocket(socket))
                        .ContinueWith(x => { if (x.Exception != null) { Logger.Error(x.Exception.ToString()); } });
#pragma warning restore CS4014
                }
                catch (Exception e) {
                    Logger.Error($"Error occured while setting up socket handler? {e}");
                }
            }
        }
        catch (OperationCanceledException) {
            // ignore the exception, it's just for closing the server

            Logger.Info("Server closing");

            try {
                serverSocket.Shutdown(SocketShutdown.Both);
            }
            catch {
                // ignored
            }
            finally {
                serverSocket.Close();
            }

            Logger.Info("Server closed");
            Console.WriteLine("\n\n\n"); //for the sake of the restart command.
        }
    }

    public static void FillPacket<T>(PacketHeader header, T packet, Memory<byte> memory) where T : struct, IPacket {
        Span<byte> data = memory.Span;

        header.Serialize(data[..Constants.HeaderSize]);
        packet.Serialize(data[Constants.HeaderSize..]);
    }

    // broadcast packets to all clients
    public delegate void PacketReplacer<in T>(Client from, Client to, T value); // replacer must send

    public void BroadcastReplace<T>(T packet, Client sender, PacketReplacer<T> packetReplacer) where T : struct, IPacket {
        foreach (Client client in Clients.Where(c => c.Connected && !c.Ignored && sender.Id != c.Id)) {
            packetReplacer(sender, client, packet);
        }
    }

    public async Task Broadcast<T>(T packet, Client sender) where T : struct, IPacket {
        IMemoryOwner<byte> memory = MemoryPool<byte>.Shared.RentZero(Constants.HeaderSize + packet.Size);
        PacketHeader header = new PacketHeader {
            Id         = sender?.Id ?? Guid.Empty,
            Type       = Constants.PacketMap[typeof(T)].Type,
            PacketSize = packet.Size,
        };
        FillPacket(header, packet, memory.Memory);
        await Broadcast(memory, sender);
    }

    public Task Broadcast<T>(T packet) where T : struct, IPacket {
        return Task.WhenAll(Clients.Where(c => c.Connected && !c.Ignored).Select(async client => {
            IMemoryOwner<byte> memory = MemoryPool<byte>.Shared.RentZero(Constants.HeaderSize + packet.Size);
            PacketHeader header = new PacketHeader {
                Id         = client.Id,
                Type       = Constants.PacketMap[typeof(T)].Type,
                PacketSize = packet.Size,
            };
            FillPacket(header, packet, memory.Memory);
            await client.Send(memory.Memory, client);
            memory.Dispose();
        }));
    }

    /// <summary>
    ///     Takes ownership of data and disposes once done.
    /// </summary>
    /// <param name="data">Memory owner to dispose once done</param>
    /// <param name="sender">Optional sender to not broadcast data to</param>
    public async Task Broadcast(IMemoryOwner<byte> data, Client? sender = null) {
        await Task.WhenAll(Clients.Where(c => c.Connected && !c.Ignored && c != sender).Select(client => client.Send(data.Memory, sender)));
        data.Dispose();
    }

    /// <summary>
    ///     Broadcasts memory whose memory shouldn't be disposed, should only be fired by server code.
    /// </summary>
    /// <param name="data">Memory to send to the clients</param>
    /// <param name="sender">Optional sender to not broadcast data to</param>
    public async void Broadcast(Memory<byte> data, Client? sender = null) {
        await Task.WhenAll(Clients.Where(c => c.Connected && !c.Ignored && c != sender).Select(client => client.Send(data, sender)));
    }

    public Client? FindExistingClient(Guid id) {
        return Clients.Find(client => client.Id == id);
    }


    private async void HandleSocket(Socket socket) {
        Client client = new Client(socket) {Server = this};
        var remote = socket.RemoteEndPoint;
        IMemoryOwner<byte> memory = null!;

        bool first = true;
        try {
            while (true) {
                memory = memoryPool.Rent(Constants.HeaderSize);

                async Task<bool> Read(Memory<byte> readMem, int readSize, int readOffset) {
                    readSize += readOffset;
                    while (readOffset < readSize) {
                        int size = await socket.ReceiveAsync(readMem[readOffset..readSize], SocketFlags.None);
                        if (size == 0) {
                            // treat it as a disconnect and exit
                            Logger.Info($"Socket {remote} disconnected.");
                            if (socket.Connected) await socket.DisconnectAsync(false);
                            return false;
                        }

                        readOffset += size;
                    }

                    return true;
                }

                if (!await Read(memory.Memory[..Constants.HeaderSize], Constants.HeaderSize, 0)) {
                    break;
                }
                PacketHeader header = GetHeader(memory.Memory.Span[..Constants.HeaderSize]);
                if (first && await JsonApi.JsonApi.HandleAPIRequest(this, socket, header, memory)) { goto close; }

                Range packetRange = Constants.HeaderSize..(Constants.HeaderSize + header.PacketSize);
                if (header.PacketSize > 0) {
                    IMemoryOwner<byte> memTemp = memory; // header to copy to new memory
                    memory = memoryPool.Rent(Constants.HeaderSize + header.PacketSize);
                    memTemp.Memory.Span[..Constants.HeaderSize].CopyTo(memory.Memory.Span[..Constants.HeaderSize]);
                    memTemp.Dispose();
                    if (!await Read(memory.Memory, header.PacketSize, Constants.HeaderSize)) {
                        break;
                    }
                }

                // connection initialization
                if (first) {
                    first = false; // only do this once

                    // first client packet has to be the client init
                    if (header.Type != PacketType.Connect) {
                        throw new Exception($"First packet was not init, instead it was {header.Type} ({remote})");
                    }

                    ConnectPacket connect = new ConnectPacket();
                    connect.Deserialize(memory.Memory.Span[packetRange]);

                    client.Id   = header.Id;
                    client.Name = connect.ClientName;

                    // is the IPv4 address banned?
                    if (BanLists.Enabled && BanLists.IsIPv4Banned(((IPEndPoint) socket.RemoteEndPoint!).Address!)) {
                        Logger.Warn($"Ignoring banned IPv4 address for {client.Name} ({client.Id}/{remote})");
                        client.Ignored = true;
                        client.Banned  = true;
                    }
                    // is the profile ID banned?
                    else if (BanLists.Enabled && BanLists.IsProfileBanned(client.Id)) {
                        client.Logger.Warn($"Ignoring banned profile ID for {client.Name} ({client.Id}/{remote})");
                        client.Ignored = true;
                        client.Banned  = true;
                    }
                    // is the server full?
                    else if (Clients.Count(x => x.Connected) >= Settings.Instance.Server.MaxPlayers) {
                        client.Logger.Error($"Ignoring player {client.Name} ({client.Id}/{remote}) as server reached max players of {Settings.Instance.Server.MaxPlayers}");
                        client.Ignored = true;
                    }

                    // send server init (required to crash ignored players later)
                    await client.Send(new InitPacket {
                        MaxPlayers = (client.Ignored ? (ushort) 1 : Settings.Instance.Server.MaxPlayers),
                    });

                    // don't init or announce an ignored client to other players any further
                    if (client.Ignored) {
                        memory.Dispose();
                        continue;
                    }

                    bool wasFirst = connect.ConnectionType == ConnectPacket.ConnectionTypes.FirstConnection;

                    // add client to the set of connected players
                    lock (Clients) {
                        // is the server full? (check again, to prevent race conditions)
                        if (Clients.Count(x => x.Connected) >= Settings.Instance.Server.MaxPlayers) {
                            client.Logger.Error($"Ignoring player {client.Name} ({client.Id}/{remote}) as server reached max players of {Settings.Instance.Server.MaxPlayers}");
                            client.Ignored = true;
                            memory.Dispose();
                            continue;
                        }

                        // detect and handle reconnections
                        bool isClientNew = true;
                        switch (connect.ConnectionType) {
                            case ConnectPacket.ConnectionTypes.FirstConnection:
                            case ConnectPacket.ConnectionTypes.Reconnecting: {
                                if (FindExistingClient(client.Id) is { } oldClient) {
                                    isClientNew = false;
                                    client = new Client(oldClient, socket);
                                    client.Name = connect.ClientName;
                                    Clients.Remove(oldClient);
                                    Clients.Add(client);
                                    if (oldClient.Connected) {
                                        oldClient.Logger.Info($"Disconnecting already connected client {oldClient.Socket?.RemoteEndPoint} for {client.Socket?.RemoteEndPoint}");
                                        oldClient.Dispose();
                                    }
                                }
                                else {
                                    connect.ConnectionType = ConnectPacket.ConnectionTypes.FirstConnection;
                                }

                                break;
                            }
                            default: {
                                throw new Exception($"Invalid connection type {connect.ConnectionType} for {client.Name} ({client.Id}/{remote})");
                            }
                        }

                        client.Connected = true;

                        if (isClientNew) {
                            // do any cleanup required when it comes to new clients
                            List<Client> toDisconnect = Clients.FindAll(c => c.Id == client.Id && c.Connected && c.Socket != null);
                            Clients.RemoveAll(c => c.Id == client.Id);

                            Clients.Add(client);

                            Parallel.ForEachAsync(toDisconnect, (c, token) => c.Socket!.DisconnectAsync(false, token));
                            // done disconnecting and removing stale clients with the same id

                            ClientJoined?.Invoke(client, connect);
                        }
                        // a known client reconnects, but with a new first connection (e.g. after a restart)
                        else if (wasFirst) {
                            client.CleanMetadataOnNewConnection();
                        }
                    }

                    // for all other clients that are already connected
                    List<Client> otherConnectedPlayers = Clients.FindAll(c => c.Id != client.Id && c.Connected && c.Socket != null);
                    await Parallel.ForEachAsync(otherConnectedPlayers, async (other, _) => {
                        IMemoryOwner<byte> tempBuffer = MemoryPool<byte>.Shared.RentZero(Constants.HeaderSize + (other.CurrentCostume.HasValue ? Math.Max(connect.Size, other.CurrentCostume.Value.Size) : connect.Size));

                        // make the other client known to the new client
                        PacketHeader connectHeader = new PacketHeader {
                            Id         = other.Id,
                            Type       = PacketType.Connect,
                            PacketSize = connect.Size,
                        };
                        connectHeader.Serialize(tempBuffer.Memory.Span[..Constants.HeaderSize]);
                        ConnectPacket connectPacket = new ConnectPacket {
                            ConnectionType = ConnectPacket.ConnectionTypes.FirstConnection, // doesn't matter what it is
                            MaxPlayers     = Settings.Instance.Server.MaxPlayers,
                            ClientName     = other.Name,
                        };
                        connectPacket.Serialize(tempBuffer.Memory.Span[Constants.HeaderSize..]);
                        await client.Send(tempBuffer.Memory[..(Constants.HeaderSize + connect.Size)], null);

                        // tell the new client what costume the other client has
                        if (other.CurrentCostume.HasValue) {
                            connectHeader.Type       = PacketType.Costume;
                            connectHeader.PacketSize = other.CurrentCostume.Value.Size;
                            connectHeader.Serialize(tempBuffer.Memory.Span[..Constants.HeaderSize]);
                            other.CurrentCostume.Value.Serialize(tempBuffer.Memory.Span[Constants.HeaderSize..(Constants.HeaderSize + connectHeader.PacketSize)]);
                            await client.Send(tempBuffer.Memory[..(Constants.HeaderSize + connectHeader.PacketSize)], null);
                        }

                        tempBuffer.Dispose();

                        // make the other client reset their puppet cache for this new client, if it is a new connection (after restart)
                        if (wasFirst) {
                            await SendEmptyPackets(client, other);
                        }
                    });

                    Logger.Info($"Client {client.Name} ({client.Id}/{remote}) connected.");

                    // send missing or outdated packets from others to the new client
                    await ResendPackets(client);
                }
                else if (header.Id != client.Id && client.Id != Guid.Empty) {
                    throw new Exception($"Client {client.Name} sent packet with invalid client id {header.Id} instead of {client.Id}");
                }

                try {
                    // parse the packet
                    IPacket packet = (IPacket) Activator.CreateInstance(Constants.PacketIdMap[header.Type])!;
                    packet.Deserialize(memory.Memory.Span[Constants.HeaderSize..(Constants.HeaderSize + packet.Size)]);

                    // process the packet
                    if (PacketHandler?.Invoke(client, packet) is false) {
                        // don't broadcast the packet to everyone
                        memory.Dispose();
                        continue;
                    }
                }
                catch (Exception e) {
                    client.Logger.Error($"Packet handler warning: {e}");
                }

#pragma warning disable CS4014
                // broadcast the packet to everyone
                Broadcast(memory, client)
                    .ContinueWith(x => { if (x.Exception != null) { Logger.Error(x.Exception.ToString()); } });
#pragma warning restore CS4014
            }
        }
        catch (Exception e) {
            if (e is SocketException {SocketErrorCode: SocketError.ConnectionReset}) {
                client.Logger.Info($"Disconnected from the server: Connection reset");
            }
            else {
                client.Logger.Error($"Disconnecting due to exception: {e}");
                if (socket.Connected) {
#pragma warning disable CS4014
                    Task.Run(() => socket.DisconnectAsync(false))
                        .ContinueWith(x => { if (x.Exception != null) { Logger.Error(x.Exception.ToString()); } });
#pragma warning restore CS4014
                }
            }

            memory?.Dispose();
        }

        // client disconnected
        if (client.Name != "Unknown User" && client.Id != Guid.Parse("00000000-0000-0000-0000-000000000000")) {
            Logger.Info($"Client {remote} ({client.Name}/{client.Id}) disconnected from the server");
        }
        else {
            Logger.Info($"Client {remote} disconnected from the server");
        }

        close:
        bool wasConnected = client.Connected;
        client.Connected = false;
        try {
            client.Dispose();
        }
        catch { /*lol*/ }

#pragma warning disable CS4014
        if (wasConnected) {
            Task.Run(() => Broadcast(new DisconnectPacket(), client))
                .ContinueWith(x => { if (x.Exception != null) { Logger.Error(x.Exception.ToString()); } });
        }
#pragma warning restore CS4014
    }

    private async Task ResendPackets(Client client) {
        async Task trySendPack<T>(Client other, T? packet) where T : struct, IPacket {
            if (packet == null) { return; }
            try {
                await client.Send((T) packet, other);
            }
            catch {
                // lol who gives a fuck
            }
        };
        async Task trySendMeta<T>(Client other, string packetType) where T : struct, IPacket {
            if (!other.Metadata.ContainsKey(packetType)) { return; }
            await trySendPack<T>(other, (T) other.Metadata[packetType]!);
        };
        await Parallel.ForEachAsync(this.ClientsConnected, async (other, _) => {
            if (client.Id == other.Id) { return; }
            await trySendMeta<CostumePacket>(other, "lastCostumePacket");
            await trySendMeta<CapturePacket>(other, "lastCapturePacket");
            await trySendPack<TagPacket>(other, other.GetTagPacket());
            await trySendMeta<GamePacket>(other, "lastGamePacket");
            await trySendMeta<PlayerPacket>(other, "lastPlayerPacket");
        });
    }

    private async Task SendEmptyPackets(Client client, Client other) {
        await other.Send(new TagPacket {
            GameMode   = GameMode.Legacy,
            UpdateType = TagPacket.TagUpdate.Both,
            IsIt       = false,
            Seconds    = 0,
            Minutes    = 0,
        }, client);
        await other.Send(new CapturePacket {
            ModelName = "",
        }, client);
    }

    private static PacketHeader GetHeader(Span<byte> data) {
        //no need to error check, the client will disconnect when the packet is invalid :)
        PacketHeader header = new PacketHeader();
        header.Deserialize(data[..Constants.HeaderSize]);
        return header;
    }
}
