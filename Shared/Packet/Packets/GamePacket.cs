using System.Runtime.InteropServices;
using System.Text;

namespace Shared.Packet.Packets;

[Packet(PacketType.Game)]
public struct GamePacket : IPacket {
    private const int StageSize = 0x40;
    public bool Is2d = false;
    public byte ScenarioNum = 0;
    public string Stage = "";

    public GamePacket() { }

    public short Size => 0x42;
    public void Serialize(Span<byte> data) {
        MemoryMarshal.Write(data, ref Is2d);
        MemoryMarshal.Write(data[1..], ref ScenarioNum);
        Encoding.UTF8.GetBytes(Stage).CopyTo(data[2..(2 + StageSize)]);
    }

    public void Deserialize(ReadOnlySpan<byte> data) {
        Is2d = MemoryMarshal.Read<bool>(data);
        ScenarioNum = MemoryMarshal.Read<byte>(data[1..]);
        Stage = Encoding.UTF8.GetString(data[2..(2 + StageSize)]).TrimEnd('\0');
    }
}