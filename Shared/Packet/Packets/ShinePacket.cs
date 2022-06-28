using System.Runtime.InteropServices;

namespace Shared.Packet.Packets;

[Packet(PacketType.Shine)]
public struct ShinePacket : IPacket {
    public int ShineId;
    public bool IsGrand;

    public short Size => 5;

    public void Serialize(Span<byte> data) {
        MemoryMarshal.Write(data, ref ShineId);
        MemoryMarshal.Write(data[1..], ref IsGrand);
    }

    public void Deserialize(ReadOnlySpan<byte> data) {
        ShineId = MemoryMarshal.Read<int>(data);
        IsGrand = MemoryMarshal.Read<bool>(data[1..]);
    }
}