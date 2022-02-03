using System.Buffers;
using System.Net.Sockets;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Shared;
using Shared.Packet;
using Shared.Packet.Packets;

TcpClient client = new TcpClient("127.0.0.1", 1027);
Guid ownId = new Guid();
Guid otherId = Guid.Empty;
Logger logger = new Logger("Client");
NetworkStream stream = client.GetStream();
PacketHeader coolHeader = new PacketHeader {
    Type = PacketType.Connect,
    Sender = PacketSender.Client,
    Id = ownId
};

int e = 0;
double d = 0;

async Task S() {
    IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.Rent(256);
    while (true) {
        await stream.ReadAsync(owner.Memory);
        PacketHeader header = MemoryMarshal.Read<PacketHeader>(owner.Memory.Span);
        if (header.Type == PacketType.Player) {
            if (otherId == Guid.Empty) otherId = header.Id;
            if (otherId != header.Id) continue;
            if (e++ != 0) {
                e %= 3;
                continue;
            }

            d += Math.PI / 180;
            unsafe {
                MemoryMarshal.Write(owner.Memory.Span, ref coolHeader);
                // unbelievably shitty way to marshal playerpacket
                fixed (byte* basePtr = owner.Memory.Span) {
                    byte* dataPtr = basePtr + Constants.HeaderSize;
                    Vector3 pos = Unsafe.Read<Vector3>(dataPtr);
                    pos.X += 1000f * (float)Math.Cos(d);
                    pos.Z += 1000f * (float)Math.Sin(d);
                    Unsafe.Write(dataPtr, pos);
                }
            }
            Console.WriteLine($"aargh {coolHeader.Id} {owner.Memory.Span[..256].Hex()}");
            await stream.WriteAsync(owner.Memory);
        }
    }
}
IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.Rent(256);
MemoryMarshal.Write(owner.Memory.Span[..], ref coolHeader);
ConnectPacket connect = new ConnectPacket {
    ConnectionType = ConnectionTypes.FirstConnection
};
MemoryMarshal.Write(owner.Memory.Span[Constants.HeaderSize..256], ref connect);
await stream.WriteAsync(owner.Memory);
coolHeader.Type = PacketType.Player;
MemoryMarshal.Write(owner.Memory.Span[..], ref coolHeader);
await S();