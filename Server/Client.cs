using System.Buffers;
using System.Collections.Concurrent;
using System.Net.Sockets;
using Shared;
using Shared.Packet;
using Shared.Packet.Packets;

namespace Server;

public class Client : IDisposable {
    public readonly ConcurrentDictionary<string, object> Metadata = new ConcurrentDictionary<string, object>(); // can be used to store any information about a player
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
        Logger = new Logger(socket.RemoteEndPoint?.ToString() ?? "Unknown User???");
    }
    
    public void Dispose() {
        if (Socket?.Connected is true)
            Socket.Disconnect(false);
    }

    public async Task Send<T>(T packet, Client? sender = null) where T : struct, IPacket {
        IMemoryOwner<byte> memory = MemoryPool<byte>.Shared.Rent(Constants.MaxPacketSize);

        PacketHeader header = new PacketHeader {
            Id = sender?.Id ?? Guid.Empty,
            Type = Constants.PacketMap[typeof(T)].Type
        };
        Server.FillPacket(header, packet, memory.Memory);
    }

    public async Task Send(ReadOnlyMemory<byte> data, Client? sender) {
        if (!Connected) {
            Server.Logger.Info($"Didn't send {(PacketType) data.Span[16]} to {Id} because they weren't connected yet");
            return;
        }

        // Server.Logger.Info($"Sending {(PacketType) data.Span[16]} to {Id} from {other?.Id.ToString() ?? "server"}");
        await Socket!.SendAsync(data[..Constants.MaxPacketSize], SocketFlags.None);
    }

    public static bool operator ==(Client? left, Client? right) {
        return left is { } leftClient && right is { } rightClient && leftClient.Id == rightClient.Id;
    }

    public static bool operator !=(Client? left, Client? right) {
        return !(left == right);
    }
}