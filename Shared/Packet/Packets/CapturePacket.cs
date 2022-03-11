using System.Runtime.InteropServices;
using System.Text;

namespace Shared.Packet.Packets;

[Packet(PacketType.Capture)]
public struct CapturePacket : IPacket {
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Constants.CostumeNameSize)]
    public string ModelName;

    public bool IsCaptured;

    public short Size => Constants.CostumeNameSize + 1;
    public void Serialize(Span<byte> data) {
        Encoding.UTF8.GetBytes(ModelName).CopyTo(data[..Constants.CostumeNameSize]);
        MemoryMarshal.Write(data[Constants.CostumeNameSize..], ref IsCaptured);
    }

    public void Deserialize(Span<byte> data) {
        ModelName = Encoding.UTF8.GetString(data[..Constants.CostumeNameSize]).TrimNullTerm();
        IsCaptured = MemoryMarshal.Read<bool>(data[Constants.CostumeNameSize..]);
    }

}