using System.Runtime.InteropServices;
using Shared.Packet.Packets;

namespace Shared.Packet;

[StructLayout(LayoutKind.Sequential)]
public struct PacketHeader : IPacket {
    // public int Length;
    public Guid Id;
    public PacketType Type;
    public short PacketSize;
    
    public short Size => 20;

    public void Serialize(Span<byte> data) {
        // MemoryMarshal.Write(data, ref Length);
        MemoryMarshal.Write(data, ref Id);
        MemoryMarshal.Write(data[16..], ref Type);
    }

    public void Deserialize(Span<byte> data) {
        Id = MemoryMarshal.Read<Guid>(data);
        Type = MemoryMarshal.Read<PacketType>(data[16..]);
        Type = MemoryMarshal.Read<PacketType>(data[16..]);
    }
}