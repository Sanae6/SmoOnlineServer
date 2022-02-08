namespace Shared.Packet.Packets; 

[Packet(PacketType.Unknown)] // empty like boss
[Packet(PacketType.Command)]
public struct UnhandledPacket : IPacket {
    public byte[] Data = new byte[Constants.PacketDataSize];
    public void Serialize(Span<byte> data) {
        Data.CopyTo(data);
    }

    public void Deserialize(Span<byte> data) {
        data.CopyTo(Data);
    }
}