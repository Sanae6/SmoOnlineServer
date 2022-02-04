namespace Shared.Packet.Packets; 

[Packet(PacketType.Command)]
public struct CommandPacket : IPacket {
    //todo: implement something for this
    public void Serialize(Span<byte> data) {
        
    }

    public void Deserialize(Span<byte> data) {
        
    }
}