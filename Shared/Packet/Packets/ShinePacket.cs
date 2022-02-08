using System.Runtime.InteropServices;

namespace Shared.Packet.Packets; 

[Packet(PacketType.Shine)]
public struct ShinePacket : IPacket {
    public int ShineId;
    public bool IsGrand;
    public void Serialize(Span<byte> data) {
        MemoryMarshal.Write(data, ref ShineId);
        MemoryMarshal.Write(data, ref IsGrand);
    }

    public void Deserialize(Span<byte> data) {
        ShineId = MemoryMarshal.Read<int>(data);
        IsGrand = MemoryMarshal.Read<bool>(data);
    }
}