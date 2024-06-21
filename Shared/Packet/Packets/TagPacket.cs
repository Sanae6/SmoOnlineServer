using System.Runtime.InteropServices;

namespace Shared.Packet.Packets;

[Packet(PacketType.Tag)]
public struct TagPacket : IPacket {
    public TagUpdate UpdateType;
    public bool IsIt;
    public byte Seconds;
    public ushort Minutes;

    public short Size => 6;

    public void Serialize(Span<byte> data) {
        MemoryMarshal.Write(data, ref UpdateType);
        MemoryMarshal.Write(data[1..], ref IsIt);
        MemoryMarshal.Write(data[2..], ref Seconds);
        MemoryMarshal.Write(data[3..], ref Minutes);
    }

    public void Deserialize(ReadOnlySpan<byte> data) {
        UpdateType = MemoryMarshal.Read<TagUpdate>(data);
        IsIt = MemoryMarshal.Read<bool>(data[1..]);
        Seconds = MemoryMarshal.Read<byte>(data[2..]);
        Minutes = MemoryMarshal.Read<ushort>(data[3..]);
    }

    [Flags]
    public enum TagUpdate : byte {
        Time = 1,
        State = 2
    }
}