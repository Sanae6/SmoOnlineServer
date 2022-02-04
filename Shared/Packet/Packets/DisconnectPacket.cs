namespace Shared.Packet.Packets; 

[Packet(PacketType.Disconnect)]
public struct DisconnectPacket : IPacket {
    //empty packet
    public void Serialize(Span<byte> data) {
        
    }

    public void Deserialize(Span<byte> data) {
        
    }
}