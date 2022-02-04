using System.Runtime.InteropServices;

namespace Shared.Packet.Packets; 

[Packet(PacketType.Costume)]
public struct CostumePacket : IPacket {
    public const int CostumeNameSize = 0x20;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CostumeNameSize)]
    public string BodyName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CostumeNameSize)]
    public string CapName;
    public void Serialize(Span<byte> data) {
        Span<char> strData = MemoryMarshal.Cast<byte, char>(data);
        BodyName.CopyTo(strData[..CostumeNameSize]);
        CapName.CopyTo(strData[CostumeNameSize..]);
    }

    public void Deserialize(Span<byte> data) {
        Span<char> strData = MemoryMarshal.Cast<byte, char>(data);
        BodyName = new string(strData[..CostumeNameSize].TrimEnd('\0'));
        CapName = new string(strData[CostumeNameSize..].TrimEnd('\0'));
    }
}