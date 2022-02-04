using System.Runtime.InteropServices;

namespace Shared.Packet.Packets; 

// Packet interface for type safety
public interface IPacket {
    void Serialize(Span<byte> data);
    void Deserialize(Span<byte> data);
}