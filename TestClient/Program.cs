using System.Buffers;
using System.Net.Sockets;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Shared;
using Shared.Packet;
using Shared.Packet.Packets;

TcpClient client = new TcpClient("127.0.0.1", 1027);
Guid ownId = new Guid(1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
Guid otherId = Guid.Empty;
Logger logger = new Logger("Client");
NetworkStream stream = client.GetStream();

int e = 0;
double d = 0;
Vector3 basePoint = Vector3.Zero;
PlayerPacket? playerPacket = null;

PacketType[] reboundPackets = {
    PacketType.Player,
    PacketType.Cap,
    PacketType.Capture,
    PacketType.Costume,
    PacketType.Tag,
    PacketType.Shine
};

async Task S() {
    IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.Rent(256);
    while (true) {
        await stream.ReadAsync(owner.Memory);
        PacketHeader header = MemoryMarshal.Read<PacketHeader>(owner.Memory.Span);
        PacketType type = header.Type;
        if (reboundPackets.All(x => x != type)) continue;
        header.Id = ownId;
        MemoryMarshal.Write(owner.Memory.Span, ref header);
        await stream.WriteAsync(owner.Memory);
    }
}

PacketHeader coolHeader = new PacketHeader {
    Type = PacketType.Connect,
    Id = ownId
};
IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.Rent(256);
MemoryMarshal.Write(owner.Memory.Span[..], ref coolHeader);
ConnectPacket connect = new ConnectPacket {
    ConnectionType = ConnectionTypes.FirstConnection
};
MemoryMarshal.Write(owner.Memory.Span[Constants.HeaderSize..256], ref connect);
await stream.WriteAsync(owner.Memory);
coolHeader.Type = PacketType.Player;
MemoryMarshal.Write(owner.Memory.Span[..], ref coolHeader);
logger.Info("Connected");
await S();