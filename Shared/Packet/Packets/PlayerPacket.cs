using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace Shared.Packet.Packets;

[Packet(PacketType.Player)]
public struct PlayerPacket : IPacket {
    public const int NameSize = 0x20;

    public Vector3 Position;
    public Quaternion Rotation;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
    public float[] AnimationBlendWeights;

    public float AnimationRate;
    public bool Is2d;
    public bool ThrowingCap;
    public bool IsIt;
    public int ScenarioNum;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x40)]
    public string Stage = "";

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = NameSize)]
    public string Act = "";

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = NameSize)]
    public string SubAct = "";

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = NameSize)]
    public string Hack = "";

    public void Serialize(Span<byte> data) {
        int offset = 0;
        MemoryMarshal.Write(data, ref Position);
        offset += Marshal.SizeOf<Vector3>();
        MemoryMarshal.Write(data[offset..], ref Rotation);
        offset += Marshal.SizeOf<Quaternion>();
        AnimationBlendWeights.CopyTo(MemoryMarshal.Cast<byte, float>(data[offset..(offset += 4 * 6)]));
        MemoryMarshal.Write(data[offset..], ref AnimationRate);
        offset += 4;
        MemoryMarshal.Write(data[offset++..], ref Is2d);
        MemoryMarshal.Write(data[offset++..], ref ThrowingCap);
        MemoryMarshal.Write(data[offset++..], ref IsIt);
        MemoryMarshal.Write(data[offset..], ref ScenarioNum);
        offset += 5;
        Encoding.UTF8.GetBytes(Stage).CopyTo(data[offset..(offset + 0x40)]);
        offset += 0x40;
        Encoding.UTF8.GetBytes(Act).CopyTo(data[offset..(offset + NameSize)]);
        offset += NameSize;
        Encoding.UTF8.GetBytes(SubAct).CopyTo(data[offset..(offset + NameSize)]);
        offset += NameSize;
        Encoding.UTF8.GetBytes(Hack).CopyTo(data[offset..]);
    }

    public void Deserialize(Span<byte> data) {
        int offset = 0;
        Position = MemoryMarshal.Read<Vector3>(data);
        offset += Marshal.SizeOf<Vector3>();
        Rotation = MemoryMarshal.Read<Quaternion>(data[offset..]);
        offset += Marshal.SizeOf<Quaternion>();
        AnimationBlendWeights = MemoryMarshal.Cast<byte, float>(data[offset..(offset += 4 * 6)]).ToArray();
        AnimationRate = MemoryMarshal.Read<float>(data[offset..(offset += 4)]);
        Is2d = MemoryMarshal.Read<bool>(data[offset++..]);
        ThrowingCap = MemoryMarshal.Read<bool>(data[offset++..]);
        IsIt = MemoryMarshal.Read<bool>(data[offset++..]);
        // offset++; // padding
        ScenarioNum = MemoryMarshal.Read<int>(data[offset..]);
        offset += 5;
        Stage = Encoding.UTF8.GetString(data[offset..(offset + 0x40)]).TrimEnd('\0');
        offset += 0x40;
        Act = Encoding.UTF8.GetString(data[offset..(offset + NameSize)]).TrimEnd('\0');
        offset += NameSize;
        SubAct = Encoding.UTF8.GetString(data[offset..(offset + NameSize)]).TrimEnd('\0');
        offset += NameSize;
        SubAct = Encoding.UTF8.GetString(data[offset..(offset + NameSize)]).TrimEnd('\0');
    }
}