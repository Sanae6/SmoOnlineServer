using System.Buffers;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Shared;
using Shared.Packet;
using Shared.Packet.Packets;

namespace Server;

public class Server {
    private readonly MemoryPool<byte> memoryPool = MemoryPool<byte>.Shared;
    public readonly List<Client> Clients = new List<Client>();
    public readonly Logger Logger = new Logger("Server");
    private static int HeaderSize => Marshal.SizeOf<PacketHeader>();
    public async Task Listen(ushort port) {
        TcpListener listener = TcpListener.Create(port);

        listener.Start();

        while (true) {
            Socket socket = await listener.AcceptSocketAsync();

            if (Clients.Count > Constants.MaxClients) {
                Logger.Warn("Turned away client due to max clients");
                await socket.DisconnectAsync(false);
                continue;
            }

            HandleSocket(socket);
        }
    }

    public static void FillPacket<T>(PacketHeader header, T packet, Memory<byte> memory) where T : unmanaged, IPacket {
        Span<byte> data = memory.Span;

        MemoryMarshal.Write(data, ref header);
        MemoryMarshal.Write(data[HeaderSize..], ref packet);
    }

    // broadcast packets to all clients
    public async void Broadcast<T>(T packet, Client? sender = null) where T : unmanaged, IPacket {
        IMemoryOwner<byte> memory = memoryPool.Rent(Marshal.SizeOf<T>() + HeaderSize);

        PacketHeader header = new PacketHeader {
            Id = sender?.Id ?? Guid.Empty,
            Type = Constants.Packets[typeof(T)].Type,
            Sender = PacketSender.Server // todo maybe use client?  
        };
        FillPacket(header, packet, memory.Memory);
        await Broadcast(memory, sender);
    }

    /// <summary>
    /// Takes ownership of data and disposes once done.
    /// </summary>
    /// <param name="data">Memory owner to dispose once done</param>
    /// <param name="sender">Optional sender to not broadcast data to</param>
    public async Task Broadcast(IMemoryOwner<byte> data, Client? sender = null) {
        await Task.WhenAll(Clients.Where(c => c.Connected && c != sender).Select(client => client.Send(data.Memory)));
        data.Dispose();
    }

    /// <summary>
    /// Broadcasts memory whose memory shouldn't be disposed, should only be fired by server code.
    /// </summary>
    /// <param name="data">Memory to send to the clients</param>
    /// <param name="sender">Optional sender to not broadcast data to</param>
    public async void Broadcast(Memory<byte> data, Client? sender = null) {
        await Task.WhenAll(Clients.Where(c => c.Connected && c != sender).Select(client => client.Send(data)));
    }

    public Client? FindExistingClient(Guid id) {
        return Clients.Find(client => client.Id == id);
    }


    private async void HandleSocket(Socket socket) {
        Client client = new Client {Socket = socket};
        IMemoryOwner<byte> memory = null!;
        bool first = true;
        try {
            while (true) {
                memory = memoryPool.Rent(Constants.MaxPacketSize);
                int size = await socket.ReceiveAsync(memory.Memory, SocketFlags.None);
                if (size == 0) { // treat it as a disconnect and exit
                    Logger.Info($"Socket {socket.RemoteEndPoint} disconnected.");
                    await socket.DisconnectAsync(false);
                    break;
                }

                PacketHeader header = GetHeader(memory.Memory.Span[..size]);
                //Logger.Info($"first = {first}, type = {header.Type}, data = " + memory.Memory.Span[..size].Hex());
                // connection initialization
                if (first) {
                    first = false;
                    if (header.Type != PacketType.Connect) {
                        throw new Exception("First packet was not init");
                    }

                    ConnectPacket connect = MemoryMarshal.Read<ConnectPacket>(memory.Memory.Span[HeaderSize..size]);
                    lock (Clients) {
                        switch (connect.ConnectionType) {
                            case ConnectionTypes.FirstConnection: {
                                // do any cleanup required when it comes to new clients
                                List<Client> toDisconnect = Clients.FindAll(c => c.Id == header.Id && c.Connected && c.Socket != null);
                                Clients.RemoveAll(c => c.Id == header.Id);
                    
                                client.Id = header.Id;
                                Clients.Add(client);

                                foreach (Client c in toDisconnect) c.Socket!.DisconnectAsync(false);
                                // done disconnecting and removing stale clients with the same id
                                break;
                            }
                            case ConnectionTypes.Reconnecting: {
                                if (FindExistingClient(header.Id) is { } newClient) {
                                    if (newClient.Connected) throw new Exception($"Tried to join as already connected user {header.Id}");
                                    newClient.Socket = client.Socket;
                                    client = newClient;
                                } else {
                                    // TODO MAJOR: IF CLIENT COULD NOT BE FOUND, SERVER SHOULD GRACEFULLY TELL CLIENT THAT ON DISCONNECT
                                    // can be done via disconnect packet when that gets more data?
                                    throw new Exception("Could not find a suitable client to reconnect as");
                                }
                                break;
                            }
                            default:
                                throw new Exception($"Invalid connection type {connect.ConnectionType}");
                        }
                    }
                    
                    Logger.Info($"Client {socket.RemoteEndPoint} connected.");

                    continue;
                }

                
                // todo support variable length packets when they show up
                if (header.Sender == PacketSender.Client) await Broadcast(memory, client);
                else {
                    //todo handle server packets :)
                }
            }
        }
        catch (Exception e) {
            if (e is SocketException {SocketErrorCode: SocketError.ConnectionReset}) {
                Logger.Info($"Client {socket.RemoteEndPoint} disconnected from the server");
            } else {
                Logger.Error($"Exception on socket {socket.RemoteEndPoint}, disconnecting for: {e}");
                await socket.DisconnectAsync(false);
            }

            memory?.Dispose();
        }
        client.Dispose();
    }

    private static PacketHeader GetHeader(Span<byte> data) {
        //no need to error check, the client will disconnect when the packet is invalid :)
        return MemoryMarshal.Read<PacketHeader>(data);
    }
}