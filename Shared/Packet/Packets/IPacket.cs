namespace Shared.Packet.Packets;

// Packet interface for type safety
public interface IPacket {
    short Size { get; }
    void Serialize(Span<byte> data);
    void Deserialize(ReadOnlySpan<byte> data);
}