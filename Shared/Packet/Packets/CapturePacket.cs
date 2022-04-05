using System.Runtime.InteropServices;
using System.Text;

namespace Shared.Packet.Packets;

[Packet(PacketType.Capture)]
public struct CapturePacket : IPacket {
    public string ModelName;

    public short Size => Constants.CostumeNameSize;
    public void Serialize(Span<byte> data) {
        Encoding.UTF8.GetBytes(ModelName).CopyTo(data[..Constants.CostumeNameSize]);
    }

    public void Deserialize(ReadOnlySpan<byte> data) {
        ModelName = Encoding.UTF8.GetString(data[..Constants.CostumeNameSize]).TrimNullTerm();
    }

}