using System.Buffers;
using System.Net.Sockets;
using Shared.Packet.Packets;

namespace Server; 

public class Client : IDisposable {
    public Socket? Socket;
    public bool Connected = false;
    public Guid Id;
    public CostumePacket CurrentCostume = new CostumePacket {
        BodyName = "",
        CapName = ""
    };
    public readonly Dictionary<string, object> Metadata = new Dictionary<string, object>(); // can be used to store any information about a player

    public async Task Send(Memory<byte> data) {
        if (!Connected) return;
        await Socket!.SendAsync(data, SocketFlags.None);
    }

    public void Dispose() {
        Socket?.Disconnect(false);
    }

    public static bool operator ==(Client? left, Client? right) => left is { } leftClient && right is { } rightClient && leftClient.Id == rightClient.Id;
    public static bool operator !=(Client? left, Client? right) => !(left == right);
}