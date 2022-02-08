using System.Runtime.InteropServices;

namespace Shared.Packet.Packets; 

public class TagPacket : IPacket {
    public bool IsIt = false;
    public void Serialize(Span<byte> data) {
        MemoryMarshal.Write(data, ref IsIt);
    }

    public void Deserialize(Span<byte> data) {
        IsIt = MemoryMarshal.Read<bool>(data);
    }
}