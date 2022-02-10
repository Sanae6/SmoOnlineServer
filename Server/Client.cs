using System.Net.Sockets;
using Shared;
using Shared.Packet.Packets;

namespace Server;

public class Client : IDisposable {
    public readonly Dictionary<string, object> Metadata = new Dictionary<string, object>(); // can be used to store any information about a player
    public bool Connected = false;

    public CostumePacket CurrentCostume = new CostumePacket {
        BodyName = "",
        CapName = ""
    };

    public Guid Id;
    public Socket? Socket;

    public void Dispose() {
        Socket?.Disconnect(false);
    }

    public async Task Send(ReadOnlyMemory<byte> data) {
        if (!Connected) return;
        await Socket!.SendAsync(data[..Constants.MaxPacketSize], SocketFlags.None);
    }

    public static bool operator ==(Client? left, Client? right) {
        return left is { } leftClient && right is { } rightClient && leftClient.Id == rightClient.Id;
    }

    public static bool operator !=(Client? left, Client? right) {
        return !(left == right);
    }
}