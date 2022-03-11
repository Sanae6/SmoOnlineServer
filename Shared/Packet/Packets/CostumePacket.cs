using System.Runtime.InteropServices;
using System.Text;

namespace Shared.Packet.Packets;

[Packet(PacketType.Costume)]
public struct CostumePacket : IPacket {
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Constants.CostumeNameSize)]
    public string BodyName;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Constants.CostumeNameSize)]
    public string CapName;

    public short Size => Constants.CostumeNameSize * 2;

    public void Serialize(Span<byte> data) {
        Encoding.UTF8.GetBytes(BodyName).CopyTo(data[..Constants.CostumeNameSize]);
        Encoding.UTF8.GetBytes(CapName).CopyTo(data[Constants.CostumeNameSize..]);
    }

    public void Deserialize(Span<byte> data) {
        BodyName = Encoding.UTF8.GetString(data[..Constants.CostumeNameSize]).TrimNullTerm();
        CapName = Encoding.UTF8.GetString(data[Constants.CostumeNameSize..]).TrimNullTerm();
    }
}