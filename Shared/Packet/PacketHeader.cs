using System.Runtime.InteropServices;
using Shared.Packet.Packets;

namespace Shared.Packet;

[StructLayout(LayoutKind.Sequential)]
public struct PacketHeader : IPacket {
    // public int Length;
    public Guid Id;
    public PacketType Type;
    public short PacketSize;

    public static short StaticSize => 20;
    public short Size => StaticSize;

    public void Serialize(Span<byte> data) {
        MemoryMarshal.Write(data[..16], ref Id);
        MemoryMarshal.Write(data[16..], ref Type);
        MemoryMarshal.Write(data[18..], ref PacketSize);
    }

    public void Deserialize(ReadOnlySpan<byte> data) {
        Id = MemoryMarshal.Read<Guid>(data[..16]);
        Type = MemoryMarshal.Read<PacketType>(data[16..]);
        PacketSize = MemoryMarshal.Read<short>(data[18..]);
    }
}