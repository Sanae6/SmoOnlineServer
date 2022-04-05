using System.Buffers;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Shared;
using Shared.Packet;
using Shared.Packet.Packets;

namespace Server;

public class Client : IDisposable {
    public readonly ConcurrentDictionary<string, object?> Metadata = new ConcurrentDictionary<string, object?>(); // can be used to store any information about a player
    public bool Connected = false;
    public CostumePacket? CurrentCostume = null; // required for proper client sync
    public string Name {
        get => Logger.Name;
        set => Logger.Name = value;
    }

    public Guid Id;
    public Socket? Socket;
    public Server Server { get; init; }
    public Logger Logger { get; }

    public Client(Socket socket) {
        Socket = socket;
        Logger = new Logger("Unknown User");
    }

    public void Dispose() {
        if (Socket?.Connected is true)
            Socket.Disconnect(false);
    }

    public delegate IPacket PacketTransformerDel(Client? sender, IPacket packet);

    public event PacketTransformerDel? PacketTransformer;

    public async Task Send<T>(T packet, Client? sender = null) where T : struct, IPacket {
        IMemoryOwner<byte> memory = MemoryPool<byte>.Shared.RentZero(Constants.HeaderSize + packet.Size);
        packet = (T) (PacketTransformer?.Invoke(sender, packet) ?? packet);
        PacketHeader header = new PacketHeader {
            Id = sender?.Id ?? Id,
            Type = Constants.PacketMap[typeof(T)].Type,
            PacketSize = packet.Size
        };
        Server.FillPacket(header, packet, memory.Memory);
        await Send(memory.Memory[..], sender);
        memory.Dispose();
    }

    public async Task Send(Memory<byte> data, Client? sender) {
        if (!Connected) {
            // Server.Logger.Info($"Didn't send {MemoryMarshal.Read<PacketType>(data.Span[16..])} to {Id} because they weren't connected yet");
            return;
        }

        int packetSize = MemoryMarshal.Read<short>(data.Span[18..]);
        // if (PacketTransformer != null) {
        //     PacketType type = MemoryMarshal.Read<PacketType>(data.Span[16..]);
        //     IPacket packet = (IPacket) Activator.CreateInstance(Constants.PacketIdMap[type])!;
        //     packet.Deserialize(data.Span);
        //     packet = PacketTransformer?.Invoke(sender, packet) ?? packet;
        //     packet.Serialize(data.Span);
        // }
        await Socket!.SendAsync(data[..(Constants.HeaderSize + packetSize)], SocketFlags.None);
    }

    public static bool operator ==(Client? left, Client? right) {
        return left is { } leftClient && right is { } rightClient && leftClient.Id == rightClient.Id;
    }

    public static bool operator !=(Client? left, Client? right) {
        return !(left == right);
    }
}