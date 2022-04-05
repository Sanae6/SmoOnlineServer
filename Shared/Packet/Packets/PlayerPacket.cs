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

    public float[] AnimationBlendWeights = Array.Empty<float>();

    public ushort Act;
    public ushort SubAct;

    public PlayerPacket() {
        Position = default;
        Rotation = default;
        Act = 0;
        SubAct = 0;
    }

    public short Size => 0x38;

    public void Serialize(Span<byte> data) {
        int offset = 0;
        MemoryMarshal.Write(data[..(offset += Marshal.SizeOf<Vector3>())], ref Position);
        MemoryMarshal.Write(data[offset..(offset += Marshal.SizeOf<Quaternion>())], ref Rotation);
        AnimationBlendWeights.CopyTo(MemoryMarshal.Cast<byte, float>(data[offset..(offset += 4 * 6)]));
        MemoryMarshal.Write(data[offset++..++offset], ref Act);
        MemoryMarshal.Write(data[offset++..++offset], ref SubAct);
    }

    public void Deserialize(ReadOnlySpan<byte> data) {
        int offset = 0;
        Position = MemoryMarshal.Read<Vector3>(data[..(offset += Marshal.SizeOf<Vector3>())]);
        Rotation = MemoryMarshal.Read<Quaternion>(data[offset..(offset += Marshal.SizeOf<Quaternion>())]);
        AnimationBlendWeights = MemoryMarshal.Cast<byte, float>(data[offset..(offset += 4 * 6)]).ToArray();
        Act = MemoryMarshal.Read<ushort>(data[offset++..++offset]);
        SubAct = MemoryMarshal.Read<ushort>(data[offset++..++offset]);
    }
}