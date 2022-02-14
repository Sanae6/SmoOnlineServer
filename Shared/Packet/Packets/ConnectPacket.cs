using System.Runtime.InteropServices;
using System.Text;

namespace Shared.Packet.Packets;

[Packet(PacketType.Connect)]
public struct ConnectPacket : IPacket {
    public ConnectionTypes ConnectionType;
    public string ClientName = "?????";

    public void Serialize(Span<byte> data) {
        MemoryMarshal.Write(data, ref ConnectionType);
        Encoding.UTF8.GetBytes(ClientName).CopyTo(data[4..(4 + Constants.CostumeNameSize)]);
    }

    public void Deserialize(Span<byte> data) {
        ConnectionType = MemoryMarshal.Read<ConnectionTypes>(data);
        ClientName = Encoding.UTF8.GetString(data[4..(4 + Constants.CostumeNameSize)]).TrimNullTerm();
    }
}