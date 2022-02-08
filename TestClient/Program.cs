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

async void Funny() {
    Memory<byte> memory = new Memory<byte>(new byte[256]);

    {
        PacketHeader header = new PacketHeader {
            Id = ownId,
            Type = PacketType.Player
        };
        MemoryMarshal.Write(memory.Span, ref header);
    }

    while (true) {
        d += Math.PI / 32;
        if (playerPacket == null) {
            // wait until valid player packet has arrived
            await Task.Delay(300);
            continue;
        }

        PlayerPacket packet = playerPacket.Value;
        Vector3 pos = basePoint;
        pos.X += 100f * (float) Math.Cos(d);
        pos.Y += 300f;
        pos.Z += 100f * (float) Math.Sin(d);
        packet.Position = pos;
        packet.Serialize(memory.Span[Constants.HeaderSize..]);
        logger.Warn($"Current strs:{packet.Stage}-{packet.Act}-{packet.SubAct} {packet.Is2d} {packet.ThrowingCap} {packet.IsIt}");
        
        await stream.WriteAsync(memory);
        await Task.Delay(50);
    }
}

async Task S() {
    IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.Rent(256);
    while (true) {
        await stream.ReadAsync(owner.Memory);
        unsafe {
            
            PacketHeader header = MemoryMarshal.Read<PacketHeader>(owner.Memory.Span);
            if (header.Type == PacketType.Player) {
                if (otherId == Guid.Empty) otherId = header.Id;
                if (otherId != header.Id) continue;
                if (e++ != 0) {
                    e %= 3;
                    continue;
                }

                header.Id = ownId;
                MemoryMarshal.Write(owner.Memory.Span, ref header);
                fixed (byte* data = owner.Memory.Span[Constants.HeaderSize..]) {
                    logger.Error($"{Marshal.OffsetOf<PlayerPacket>(nameof(PlayerPacket.AnimationBlendWeights))} {Marshal.OffsetOf<PlayerPacket>(nameof(PlayerPacket.AnimationRate))}");
                    PlayerPacket packet = Marshal.PtrToStructure<PlayerPacket>((IntPtr) data);
                    playerPacket = packet;
                    basePoint = packet.Position;
                }

                // packet.SubAct = "";
            } else if (header.Type == PacketType.Cap) {
            
            }
        }
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
Task.Run(Funny);
await S();