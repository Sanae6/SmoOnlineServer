using System.Buffers;
using System.Net.Sockets;
using System.Numerics;
using System.Runtime.InteropServices;
using Shared;
using Shared.Packet;
using Shared.Packet.Packets;

TcpClient client = new TcpClient(args[0], 1027);
Guid ownId = new Guid(1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
// Guid ownId = Guid.NewGuid();
Guid otherId = Guid.Parse("d5feae62-2e71-1000-88fd-597ea147ae88");
Logger logger = new Logger("Client");
NetworkStream stream = client.GetStream();

Vector3 basePoint = Vector3.Zero;

PacketType[] reboundPackets = {
    PacketType.Player,
    PacketType.Cap,
    PacketType.Capture,
    PacketType.Costume,
    PacketType.Tag,
    PacketType.Shine
};

string lastCapture = "";

async Task S() {
    IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.Rent(Constants.MaxPacketSize);
    while (true) {
        await stream.ReadAsync(owner.Memory);
        PacketHeader header = MemoryMarshal.Read<PacketHeader>(owner.Memory.Span);
        PacketType type = header.Type;
        if (header.Id != otherId) continue;
        if (type is PacketType.Player) {
            // CapPacket cap = new CapPacket();
            PlayerPacket playerPacket = new PlayerPacket();
            playerPacket.Deserialize(owner.Memory.Span[Constants.HeaderSize..]);
            logger.Info(playerPacket.Hack);
            if (playerPacket.Hack != lastCapture) logger.Info($"Changed to hack: {lastCapture = playerPacket.Hack}");
            // cap.Position = playerPacket.Position + Vector3.UnitY * 500f;
            // cap.Rotation = Quaternion.CreateFromYawPitchRoll(0,0,0);
            // cap.CapAnim = "StayR";
            // playerPacket.Position = new Vector3(1000000f);
            // playerPacket.ThrowingCap = true;
            // header.Id = ownId;
            // MemoryMarshal.Write(owner.Memory.Span, ref header);
            // playerPacket.Serialize(owner.Memory.Span[Constants.HeaderSize..]);
            // await stream.WriteAsync(owner.Memory[..Constants.MaxPacketSize]);
            // header.Type = PacketType.Cap;
            // MemoryMarshal.Write(owner.Memory.Span, ref header);
            // cap.Serialize(owner.Memory.Span[Constants.HeaderSize..]);
            // await stream.WriteAsync(owner.Memory[..Constants.MaxPacketSize]);
            // continue;
        }

        if (reboundPackets.All(x => x != type)) continue;
        header.Id = ownId;
        MemoryMarshal.Write(owner.Memory.Span, ref header);
        await stream.WriteAsync(owner.Memory[..Constants.MaxPacketSize]);
    }
}

PacketHeader coolHeader = new PacketHeader {
    Type = PacketType.Connect,
    Id = ownId
};
IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.RentZero(Constants.MaxPacketSize);
MemoryMarshal.Write(owner.Memory.Span[..], ref coolHeader);
ConnectPacket connect = new ConnectPacket {
    ConnectionType = ConnectionTypes.Reconnecting,
    ClientName = "Test Sanae"
};
connect.Serialize(owner.Memory.Span[Constants.HeaderSize..Constants.MaxPacketSize]);
await stream.WriteAsync(owner.Memory);
logger.Info("Connected");
await S();