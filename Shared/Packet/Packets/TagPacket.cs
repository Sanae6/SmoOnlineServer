using System.Runtime.InteropServices;

namespace Shared.Packet.Packets;

[Packet(PacketType.Tag)]
public struct TagPacket : IPacket {
    public bool IsIt;

    public void Serialize(Span<byte> data) {
        MemoryMarshal.Write(data, ref IsIt);
    }

    public void Deserialize(Span<byte> data) {
        IsIt = MemoryMarshal.Read<bool>(data);
    }
}