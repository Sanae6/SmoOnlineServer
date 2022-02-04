using System.Runtime.InteropServices;

namespace Shared.Packet.Packets; 

public class GamePacket : IPacket {
    public int ScenarioNum;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = PlayerPacket.NameSize)]
    public string Stage;
    
    public void Serialize(Span<byte> data) {
        MemoryMarshal.Write(data, ref ScenarioNum);
        Stage.CopyTo(MemoryMarshal.Cast<byte, char>(data[4..]));
    }

    public void Deserialize(Span<byte> data) {
        ScenarioNum = MemoryMarshal.Read<int>(data);
        Stage = new string(MemoryMarshal.Cast<byte, char>(data[4..]));
    }
}