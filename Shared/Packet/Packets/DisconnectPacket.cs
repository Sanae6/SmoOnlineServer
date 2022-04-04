namespace Shared.Packet.Packets;

[Packet(PacketType.Disconnect)]
public struct DisconnectPacket : IPacket {
    //empty packet
    public short Size => 0;
    public void Serialize(Span<byte> data) { }

    public void Deserialize(ReadOnlySpan<byte> data) { }
}