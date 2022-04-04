using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace Shared.Packet.Packets;

[Packet(PacketType.Cap)]
public struct CapPacket : IPacket {
    public const int NameSize = 0x30;
    public Vector3 Position;
    public Quaternion Rotation;
    public bool CapOut;
    public string CapAnim;

    public short Size => 0x50;

    public void Serialize(Span<byte> data) {
        MemoryMarshal.Write(data, ref Position);
        MemoryMarshal.Write(data[12..], ref Rotation);
        MemoryMarshal.Write(data[28..], ref CapOut);
        Encoding.UTF8.GetBytes(CapAnim).CopyTo(data[32..(32 + NameSize)]);
    }

    public void Deserialize(ReadOnlySpan<byte> data) {
        Position = MemoryMarshal.Read<Vector3>(data);
        Rotation = MemoryMarshal.Read<Quaternion>(data[12..]);
        CapOut = MemoryMarshal.Read<bool>(data[28..]);
        CapAnim = Encoding.UTF8.GetString(data[32..(32 + NameSize)]).TrimEnd('\0');
    }
}