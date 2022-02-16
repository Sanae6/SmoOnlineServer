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
    public readonly Logger Logger = new Logger("Server");
    private readonly MemoryPool<byte> memoryPool = MemoryPool<byte>.Shared;
    public Func<Client, IPacket, bool>? PacketHandler = null!;
    public event Action<Client, ConnectPacket> ClientJoined = null!;

    public async Task Listen(ushort port) {
        Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        serverSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        serverSocket.Bind(new IPEndPoint(IPAddress.Any, port));
        serverSocket.Listen();

        Logger.Info($"Listening on port {port}");

        while (true) {
            Socket socket = await serverSocket.AcceptAsync();

            Logger.Warn($"Accepted connection for client {socket.RemoteEndPoint}");

            try {
                if (Clients.Count > Constants.MaxClients) {
                    Logger.Warn("Turned away client due to max clients");
                    await socket.DisconnectAsync(false);
                    continue;
                }

                Task.Run(() => HandleSocket(socket));
            } catch {
                // super ignore this
            }
        }
    }

    public static void FillPacket<T>(PacketHeader header, T packet, Memory<byte> memory) where T : struct, IPacket {
        Span<byte> data = memory.Span;

        MemoryMarshal.Write(data, ref header);
        packet.Serialize(data[Constants.HeaderSize..]);
    }

    // broadcast packets to all clients
    public async Task Broadcast<T>(T packet, Client sender) where T : struct, IPacket {
        IMemoryOwner<byte> memory = memoryPool.Rent(Constants.MaxPacketSize);

        PacketHeader header = new PacketHeader {
            Id = sender?.Id ?? Guid.Empty,
            Type = Constants.PacketMap[typeof(T)].Type
        };
        FillPacket(header, packet, memory.Memory);
        await Broadcast(memory, sender);
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
        Client client = new Client(socket) { Server = this };
        IMemoryOwner<byte> memory = null!;
        bool first = true;
        try {
            while (true) {
                memory = memoryPool.Rent(Constants.MaxPacketSize);
                {
                    int readOffset = 0;
                    while (readOffset < Constants.MaxPacketSize) {
                        int size = await socket.ReceiveAsync(memory.Memory[readOffset..Constants.MaxPacketSize], SocketFlags.None);
                        if (size == 0) {
                            // treat it as a disconnect and exit
                            Logger.Info($"Socket {socket.RemoteEndPoint} disconnected.");
                            if (socket.Connected) await socket.DisconnectAsync(false);
                            break;
                        }

                        readOffset += size;
                    }

                    if (readOffset < Constants.MaxPacketSize) break;
                }

                PacketHeader header = GetHeader(memory.Memory.Span[..Constants.MaxPacketSize]);
                //Logger.Info($"first = {first}, type = {header.Type}, data = " + memory.Memory.Span[..size].Hex());
                // connection initialization
                if (first) {
                    first = false;
                    if (header.Type != PacketType.Connect) throw new Exception($"First packet was not init, instead it was {header.Type}");

                    ConnectPacket connect = new ConnectPacket();
                    connect.Deserialize(memory.Memory.Span[Constants.HeaderSize..Constants.MaxPacketSize]);
                    lock (Clients) {
                        bool firstConn = false;
                        switch (connect.ConnectionType) {
                            case ConnectionTypes.FirstConnection: {
                                firstConn = true;
                                break;
                            }
                            case ConnectionTypes.Reconnecting: {
                                client.Id = header.Id;
                                client.Name = connect.ClientName;
                                if (FindExistingClient(header.Id) is { } newClient) {
                                    if (newClient.Connected) throw new Exception($"Tried to join as already connected user {header.Id}");
                                    newClient.Socket = client.Socket;
                                    client = newClient;
                                } else {
                                    firstConn = true;
                                    connect.ConnectionType = ConnectionTypes.FirstConnection;
                                }

                                break;
                            }
                            default:
                                throw new Exception($"Invalid connection type {connect.ConnectionType}");
                        }

                        client.Connected = true;
                        if (firstConn) {
                            // do any cleanup required when it comes to new clients
                            List<Client> toDisconnect = Clients.FindAll(c => c.Id == header.Id && c.Connected && c.Socket != null);
                            Clients.RemoveAll(c => c.Id == header.Id);

                            client.Id = header.Id;
                            Clients.Add(client);

                            Parallel.ForEachAsync(toDisconnect, (c, token) => c.Socket!.DisconnectAsync(false, token));
                            // done disconnecting and removing stale clients with the same id

                            ClientJoined?.Invoke(client, connect);
                        }
                    }

                    List<Client> otherConnectedPlayers = Clients.FindAll(c => c.Id != header.Id && c.Connected && c.Socket != null);
                    await Parallel.ForEachAsync(otherConnectedPlayers, async (other, _) => {
                        IMemoryOwner<byte> tempBuffer = MemoryPool<byte>.Shared.Rent(Constants.MaxPacketSize);
                        PacketHeader connectHeader = new PacketHeader {
                            Id = other.Id,
                            Type = PacketType.Connect
                        };
                        MemoryMarshal.Write(tempBuffer.Memory.Span, ref connectHeader);
                        ConnectPacket connectPacket = new ConnectPacket {
                            ConnectionType = ConnectionTypes.FirstConnection, // doesn't matter what it is :)
                            ClientName = other.Name
                        };
                        connectPacket.Serialize(tempBuffer.Memory.Span[Constants.HeaderSize..]);
                        await client.Send(tempBuffer.Memory, null);
                        if (other.CurrentCostume.HasValue) {
                            connectHeader.Type = PacketType.Costume;
                            MemoryMarshal.Write(tempBuffer.Memory.Span, ref connectHeader);
                            other.CurrentCostume.Value.Serialize(tempBuffer.Memory.Span[Constants.HeaderSize..]);
                            await client.Send(tempBuffer.Memory, null);
                        }

                        tempBuffer.Dispose();
                    });

                    Logger.Info($"Client {client.Name} ({client.Id}/{socket.RemoteEndPoint}) connected.");
                }

                if (header.Type == PacketType.Costume) {
                    CostumePacket costumePacket = new CostumePacket {
                        BodyName = ""
                    };
                    costumePacket.Deserialize(memory.Memory.Span[Constants.HeaderSize..]);
                    client.CurrentCostume = costumePacket;
                }

                try {
                    // if (header.Type is not PacketType.Cap and not PacketType.Player) client.Logger.Warn($"lol {header.Type}");
                    IPacket packet = (IPacket) Activator.CreateInstance(Constants.PacketIdMap[header.Type])!;
                    packet.Deserialize(memory.Memory.Span[Constants.HeaderSize..]);
                    if (PacketHandler?.Invoke(client, packet) is false) continue;
                } catch (Exception e){
                    client.Logger.Error($"Packet handler warning: {e}");
                }

                Broadcast(memory, client);
            }
        } catch (Exception e) {
            if (e is SocketException { SocketErrorCode: SocketError.ConnectionReset }) {
                client.Logger.Info($"Client {socket.RemoteEndPoint} ({client.Id}) disconnected from the server");
            } else {
                client.Logger.Error($"Exception on socket {socket.RemoteEndPoint} ({client.Id}) and disconnecting for: {e}");
                if (socket.Connected) Task.Run(() => socket.DisconnectAsync(false));
            }

            memory?.Dispose();
        }

        Clients.Remove(client);
        client.Dispose();
        Task.Run(() => Broadcast(new DisconnectPacket(), client));
    }

    private static PacketHeader GetHeader(Span<byte> data) {
        //no need to error check, the client will disconnect when the packet is invalid :)
        return MemoryMarshal.Read<PacketHeader>(data);
    }
}