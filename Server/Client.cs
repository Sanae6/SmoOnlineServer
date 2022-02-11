using System.Net.Sockets;
using Shared;
using Shared.Packet;
using Shared.Packet.Packets;

namespace Server;

public class Client : IDisposable {
    public readonly Dictionary<string, object> Metadata = new Dictionary<string, object>(); // can be used to store any information about a player
    public bool Connected = false;

    public CostumePacket? CurrentCostume;

    public Guid Id;
    public Socket? Socket;
    public Server Server { get; init; }

    public void Dispose() {
        Socket?.Disconnect(false);
    }

    public async Task Send(ReadOnlyMemory<byte> data, Client? other) {
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