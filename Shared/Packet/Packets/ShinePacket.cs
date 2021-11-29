namespace Shared.Packet.Packets; 

[Packet(PacketType.Shine)]
public struct ShinePacket : IPacket {
    public int ShineId;
}