using System.Runtime.InteropServices;

namespace Shared.Packet.Packets;

[Packet(PacketType.Tag)]
public struct TagPacket : IPacket {
    public TagUpdate UpdateType;
    public bool IsIt;
    public byte Seconds;
    public ushort Minutes;

    public short Size => 4;

    public void Serialize(Span<byte> data) {
        MemoryMarshal.Write(data, ref IsIt);
    }

    public void Deserialize(Span<byte> data) {
        IsIt = MemoryMarshal.Read<bool>(data);
    }

    [Flags]
    public enum TagUpdate : byte {
        Time = 1,
        State = 2
    }
}