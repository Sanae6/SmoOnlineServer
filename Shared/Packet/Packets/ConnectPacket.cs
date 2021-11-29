namespace Shared.Packet.Packets; 

[Packet(PacketType.Connect)]
public struct ConnectPacket : IPacket {
    public ConnectionTypes ConnectionType;
}