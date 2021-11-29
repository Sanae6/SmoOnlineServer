using System.Numerics;
using System.Runtime.InteropServices;

namespace Shared.Packet.Packets;

[Packet(PacketType.Player)]
public struct PlayerPacket : IPacket {
    public const int NameSize = 0x30;

    public Vector3 Position;
    public Quaternion Rotation;
    public float[] AnimationBlendWeights;
    public float AnimationRate;
    public bool Flat;
    public bool ThrowingCap;
    public bool Seeker;
    public int ScenarioNum;
    public string Stage;
    public string Act;
    public string SubAct;

    public void Serialize(Span<byte> data) {
        int offset = 0;
        MemoryMarshal.Write(data, ref Position);
        offset += Marshal.SizeOf<Vector3>();
        MemoryMarshal.Write(data[offset..], ref Rotation);
        offset += Marshal.SizeOf<Quaternion>();
        AnimationBlendWeights.CopyTo(MemoryMarshal.Cast<byte, float>(data[offset..(offset += 4 * 6)]));
        MemoryMarshal.Write(data[(offset += 4)..], ref AnimationRate);
        offset += 4;
        MemoryMarshal.Write(data[offset++..], ref Flat);
        MemoryMarshal.Write(data[offset++..], ref ThrowingCap);
        MemoryMarshal.Write(data[offset++..], ref Seeker);
        MemoryMarshal.Write(data[(offset += 4)..], ref ScenarioNum);
        Span<char> strData = MemoryMarshal.Cast<byte, char>(data[offset..]);
        Stage.CopyTo(strData[..NameSize]);
        Act.CopyTo(strData[NameSize..(2 * NameSize)]);
        SubAct.CopyTo(strData[(2 * NameSize)..(3 * NameSize)]);
    }

    public void Deserialize(Span<byte> data) {
        int offset = 0;
        Position = MemoryMarshal.Read<Vector3>(data);
        offset += Marshal.SizeOf<Vector3>();
        Rotation = MemoryMarshal.Read<Quaternion>(data[offset..]);
        offset += Marshal.SizeOf<Quaternion>();
        AnimationBlendWeights = MemoryMarshal.Cast<byte, float>(data[offset..(offset + 4 * 6)]).ToArray();
        offset += 4 * 6;
        AnimationRate = MemoryMarshal.Read<float>(data[(offset += 4)..]);
        offset += 4;
        Flat = MemoryMarshal.Read<bool>(data[offset++..]);
        ThrowingCap = MemoryMarshal.Read<bool>(data[offset++..]);
        Seeker = MemoryMarshal.Read<bool>(data[offset++..]);
        ScenarioNum = MemoryMarshal.Read<int>(data[(offset += 4)..]);
        Span<char> strData = MemoryMarshal.Cast<byte, char>(data[offset..]);
        Stage = new string(strData[..NameSize].TrimEnd('\0'));
        Act = new string(strData[NameSize..(2 * NameSize)].TrimEnd('\0'));
        SubAct = new string(strData[(2 * NameSize)..(3 * NameSize)].TrimEnd('\0'));
    }
}