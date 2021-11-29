using System.Runtime.InteropServices;

namespace Shared.Packet.Packets; 

[Packet(PacketType.Costume)]
public struct CostumePacket : IPacket {
    public const int CostumeNameSize = 0x20;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CostumeNameSize)]
    public string BodyName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CostumeNameSize)]
    public string CapName;
}