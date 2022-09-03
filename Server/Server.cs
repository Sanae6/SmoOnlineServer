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

                Logger.Warn($"Accepted connection for client {socket.RemoteEndPoint}");

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
        foreach (Client client in Clients.Where(client => client.Connected && sender.Id != client.Id)) packetReplacer(sender, client, packet);
    }

    public async Task Broadcast<T>(T packet, Client sender) where T : struct, IPacket {
        IMemoryOwner<byte> memory = MemoryPool<byte>.Shared.RentZero(Constants.HeaderSize + packet.Size);
        PacketHeader header = new PacketHeader {
            Id = sender?.Id ?? Guid.Empty,
            Type = Constants.PacketMap[typeof(T)].Type,
            PacketSize = packet.Size
        };
        FillPacket(header, packet, memory.Memory);
        await Broadcast(memory, sender);
    }

    public Task Broadcast<T>(T packet) where T : struct, IPacket {
        return Task.WhenAll(Clients.Where(c => c.Connected).Select(async client => {
            IMemoryOwner<byte> memory = MemoryPool<byte>.Shared.RentZero(Constants.HeaderSize + packet.Size);
            PacketHeader header = new PacketHeader {
                Id = client.Id,
                Type = Constants.PacketMap[typeof(T)].Type,
                PacketSize = packet.Size
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
        await Task.WhenAll(Clients.Where(c => c.Connected && c != sender).Select(client => client.Send(data.Memory, sender)));
        data.Dispose();
    }

    /// <summary>
    ///     Broadcasts memory whose memory shouldn't be disposed, should only be fired by server code.
    /// </summary>
    /// <param name="data">Memory to send to the clients</param>
    /// <param name="sender">Optional sender to not broadcast data to</param>
    public async void Broadcast(Memory<byte> data, Client? sender = null) {
        await Task.WhenAll(Clients.Where(c => c.Connected && c != sender).Select(client => client.Send(data, sender)));
    }

    public Client? FindExistingClient(Guid id) {
        return Clients.Find(client => client.Id == id);
    }


    private async void HandleSocket(Socket socket) {
        Client client = new Client(socket) {Server = this};
        var remote = socket.RemoteEndPoint;
        IMemoryOwner<byte> memory = null!;
        await client.Send(new InitPacket {
            MaxPlayers = Settings.Instance.Server.MaxPlayers
        });
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

                if (!await Read(memory.Memory[..Constants.HeaderSize], Constants.HeaderSize, 0))
                    break;
                PacketHeader header = GetHeader(memory.Memory.Span[..Constants.HeaderSize]);
                Range packetRange = Constants.HeaderSize..(Constants.HeaderSize + header.PacketSize);
                if (header.PacketSize > 0) {
                    IMemoryOwner<byte> memTemp = memory; // header to copy to new memory
                    memory = memoryPool.Rent(Constants.HeaderSize + header.PacketSize);
                    memTemp.Memory.Span[..Constants.HeaderSize].CopyTo(memory.Memory.Span[..Constants.HeaderSize]);
                    memTemp.Dispose();
                    if (!await Read(memory.Memory, header.PacketSize, Constants.HeaderSize))
                        break;
                }

                // connection initialization
                if (first) {
                    first = false;
                    if (header.Type != PacketType.Connect) throw new Exception($"First packet was not init, instead it was {header.Type}");

                    ConnectPacket connect = new ConnectPacket();
                    connect.Deserialize(memory.Memory.Span[packetRange]);
                    lock (Clients) {
                        if (Clients.Count(x => x.Connected) == Settings.Instance.Server.MaxPlayers) {
                            client.Logger.Error($"Turned away as server is at max clients");
                            memory.Dispose();
                            goto disconnect;
                        }

                        bool firstConn = true;
                        switch (connect.ConnectionType) {
                            case ConnectPacket.ConnectionTypes.FirstConnection:
                            case ConnectPacket.ConnectionTypes.Reconnecting: {
                                client.Id = header.Id;
                                if (FindExistingClient(header.Id) is { } oldClient) {
                                    firstConn = false;
                                    client = new Client(oldClient, socket);
                                    Clients.Remove(oldClient);
                                    Clients.Add(client);
                                    if (oldClient.Connected) {
                                        oldClient.Logger.Info($"Disconnecting already connected client {oldClient.Socket?.RemoteEndPoint} for {client.Socket?.RemoteEndPoint}");
                                        oldClient.Dispose();
                                    }
                                } else {
                                    connect.ConnectionType = ConnectPacket.ConnectionTypes.FirstConnection;
                                }

                                break;
                            }
                            default:
                                throw new Exception($"Invalid connection type {connect.ConnectionType}");
                        }

                        client.Name = connect.ClientName;
                        client.Connected = true;
                        if (firstConn) {
                            // do any cleanup required when it comes to new clients
                            List<Client> toDisconnect = Clients.FindAll(c => c.Id == header.Id && c.Connected && c.Socket != null);
                            Clients.RemoveAll(c => c.Id == header.Id);

                            Clients.Add(client);

                            Parallel.ForEachAsync(toDisconnect, (c, token) => c.Socket!.DisconnectAsync(false, token));
                            // done disconnecting and removing stale clients with the same id

                            ClientJoined?.Invoke(client, connect);
                        }
                    }

                    List<Client> otherConnectedPlayers = Clients.FindAll(c => c.Id != header.Id && c.Connected && c.Socket != null);
                    await Parallel.ForEachAsync(otherConnectedPlayers, async (other, _) => {
                        IMemoryOwner<byte> tempBuffer = MemoryPool<byte>.Shared.RentZero(Constants.HeaderSize + (other.CurrentCostume.HasValue ? Math.Max(connect.Size, other.CurrentCostume.Value.Size) : connect.Size));
                        PacketHeader connectHeader = new PacketHeader {
                            Id = other.Id,
                            Type = PacketType.Connect,
                            PacketSize = connect.Size
                        };
                        connectHeader.Serialize(tempBuffer.Memory.Span[..Constants.HeaderSize]);
                        ConnectPacket connectPacket = new ConnectPacket {
                            ConnectionType = ConnectPacket.ConnectionTypes.FirstConnection, // doesn't matter what it is
                            MaxPlayers = Settings.Instance.Server.MaxPlayers,
                            ClientName = other.Name
                        };
                        connectPacket.Serialize(tempBuffer.Memory.Span[Constants.HeaderSize..]);
                        await client.Send(tempBuffer.Memory[..(Constants.HeaderSize + connect.Size)], null);
                        if (other.CurrentCostume.HasValue) {
                            connectHeader.Type = PacketType.Costume;
                            connectHeader.PacketSize = other.CurrentCostume.Value.Size;
                            connectHeader.Serialize(tempBuffer.Memory.Span[..Constants.HeaderSize]);
                            other.CurrentCostume.Value.Serialize(tempBuffer.Memory.Span[Constants.HeaderSize..(Constants.HeaderSize + connectHeader.PacketSize)]);
                            await client.Send(tempBuffer.Memory[..(Constants.HeaderSize + connectHeader.PacketSize)], null);
                        }

                        tempBuffer.Dispose();
                    });

                    Logger.Info($"Client {client.Name} ({client.Id}/{remote}) connected.");
                } else if (header.Id != client.Id && client.Id != Guid.Empty) {
                    throw new Exception($"Client {client.Name} sent packet with invalid client id {header.Id} instead of {client.Id}");
                }

                try {
                    IPacket packet = (IPacket) Activator.CreateInstance(Constants.PacketIdMap[header.Type])!;
                    packet.Deserialize(memory.Memory.Span[Constants.HeaderSize..(Constants.HeaderSize + packet.Size)]);
                    if (PacketHandler?.Invoke(client, packet) is false) {
                        memory.Dispose();
                        continue;
                    }
                }
                catch (Exception e) {
                    client.Logger.Error($"Packet handler warning: {e}");
                }
#pragma warning disable CS4014
                Broadcast(memory, client)
                    .ContinueWith(x => { if (x.Exception != null) { Logger.Error(x.Exception.ToString()); } });
#pragma warning restore CS4014
            }
        }
        catch (Exception e) {
            if (e is SocketException {SocketErrorCode: SocketError.ConnectionReset}) {
                client.Logger.Info($"Disconnected from the server: Connection reset");
            } else {
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

        disconnect:
        if (client.Name != "Unknown User" && client.Id != Guid.Parse("00000000-0000-0000-0000-000000000000")) {
            Logger.Info($"Client {remote} ({client.Name}/{client.Id}) disconnected from the server");
        }
        else {
            Logger.Info($"Client {remote} disconnected from the server");
        }

        bool wasConnected = client.Connected;
        // Clients.Remove(client)
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

    private static PacketHeader GetHeader(Span<byte> data) {
        //no need to error check, the client will disconnect when the packet is invalid :)
        PacketHeader header = new PacketHeader();
        header.Deserialize(data[..Constants.HeaderSize]);
        return header;
    }
}
