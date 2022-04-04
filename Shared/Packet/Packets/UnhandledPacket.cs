namespace Shared.Packet.Packets;

[Packet(PacketType.Unknown)] // empty like boss
// [Packet(PacketType.Command)]
public struct UnhandledPacket : IPacket {
    public byte[] Data;

    public UnhandledPacket() {
        Data = null!;
    }
    public short Size => 0;

    public void Serialize(Span<byte> data) {
        Data.CopyTo(data);
    }

    public void Deserialize(ReadOnlySpan<byte> data) {
        Data = data.ToArray();
    }

}