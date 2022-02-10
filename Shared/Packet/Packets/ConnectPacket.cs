using System.Runtime.InteropServices;

namespace Shared.Packet.Packets;

[Packet(PacketType.Connect)]
public struct ConnectPacket : IPacket {
    public ConnectionTypes ConnectionType;

    public void Serialize(Span<byte> data) {
        MemoryMarshal.Write(data, ref ConnectionType);
    }

    public void Deserialize(Span<byte> data) {
        ConnectionType = MemoryMarshal.Read<ConnectionTypes>(data);
    }
}