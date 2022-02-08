using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace Shared.Packet.Packets; 

[Packet(PacketType.Cap)]
public struct CapPacket : IPacket {
    public const int NameSize = 0x30;
    public Vector3 Position;
    public Quaternion Rotation;
    public string CapAnim;
    public void Serialize(Span<byte> data) {
        MemoryMarshal.Write(data, ref Position);
        MemoryMarshal.Write(data[12..], ref Position);
        Encoding.UTF8.GetBytes(CapAnim).CopyTo(data[28..]);
    }

    public void Deserialize(Span<byte> data) {
        Position = MemoryMarshal.Read<Vector3>(data);
        Rotation = MemoryMarshal.Read<Quaternion>(data[12..]);
        CapAnim = Encoding.UTF8.GetString(data[28..]).TrimEnd('\0');
    }
}