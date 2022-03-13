using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace Shared.Packet.Packets;

[Packet(PacketType.Player)]
public struct PlayerPacket : IPacket {
    public const int ActSize = 0x20;
    public const int SubActSize = 0x10;

    public Vector3 Position;
    public Quaternion Rotation;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
    public float[] AnimationBlendWeights = Array.Empty<float>();

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = ActSize)]
    public string Act = "";

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = SubActSize)]
    public string SubAct = "";

    public PlayerPacket() {
        Position = default;
        Rotation = default;
    }

    public short Size => 0x64;

    public void Serialize(Span<byte> data) {
        int offset = 0;
        MemoryMarshal.Write(data[..(offset += Marshal.SizeOf<Vector3>())], ref Position);
        MemoryMarshal.Write(data[offset..(offset += Marshal.SizeOf<Quaternion>())], ref Rotation);
        AnimationBlendWeights.CopyTo(MemoryMarshal.Cast<byte, float>(data[offset..(offset += 4 * 6)]));
        Encoding.UTF8.GetBytes(Act).CopyTo(data[offset..(offset += ActSize)]);
        Encoding.UTF8.GetBytes(SubAct).CopyTo(data[offset..(offset + SubActSize)]);
    }

    public void Deserialize(Span<byte> data) {
        int offset = 0;
        Position = MemoryMarshal.Read<Vector3>(data[..(offset += Marshal.SizeOf<Vector3>())]);
        Rotation = MemoryMarshal.Read<Quaternion>(data[offset..(offset += Marshal.SizeOf<Quaternion>())]);
        AnimationBlendWeights = MemoryMarshal.Cast<byte, float>(data[offset..(offset += 4 * 6)]).ToArray();
        Act = Encoding.UTF8.GetString(data[offset..(offset += ActSize)]).TrimEnd('\0');
        SubAct = Encoding.UTF8.GetString(data[offset..(offset + SubActSize)]).TrimEnd('\0');
    }
}