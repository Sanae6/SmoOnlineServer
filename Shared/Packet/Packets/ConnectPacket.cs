using System.Runtime.InteropServices;
using System.Text;

namespace Shared.Packet.Packets;

[Packet(PacketType.Connect)]
public struct ConnectPacket : IPacket {
    public ConnectionTypes ConnectionType = ConnectionTypes.FirstConnection;
    public ushort MaxPlayers = 0;
    public string ClientName = "?????";

    public ConnectPacket() { }

    public short Size => 6 + Constants.CostumeNameSize;

    public void Serialize(Span<byte> data) {
        MemoryMarshal.Write(data, ref ConnectionType);
        MemoryMarshal.Write(data[4..], ref MaxPlayers);
        Encoding.UTF8.GetBytes(ClientName).CopyTo(data[6..(6 + Constants.CostumeNameSize)]);
    }

    public void Deserialize(ReadOnlySpan<byte> data) {
        ConnectionType = MemoryMarshal.Read<ConnectionTypes>(data);
        MaxPlayers = MemoryMarshal.Read<ushort>(data[4..]);
        ClientName = Encoding.UTF8.GetString(data[6..(6 + Constants.CostumeNameSize)]).TrimNullTerm();
    }

    public enum ConnectionTypes {
        FirstConnection,
        Reconnecting
    }
}