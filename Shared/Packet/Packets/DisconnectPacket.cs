namespace Shared.Packet.Packets; 

[Packet(PacketType.Disconnect)]
public struct DisconnectPacket : IPacket {
    public void Serialize(Span<byte> data) {
        
    }

    public void Deserialize(Span<byte> data) {
        
    }
}