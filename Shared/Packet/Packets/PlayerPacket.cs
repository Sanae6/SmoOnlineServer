using System.Numerics;
using System.Runtime.InteropServices;

namespace Shared.Packet.Packets; 

[Packet(PacketType.Player)]
public struct PlayerPacket : IPacket {
    public const int NameSize = 0x30;

    public Vector3 Position;
    public Quaternion Rotation;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
    public float[] AnimationBlendWeights;
    public float AnimationRate;
    public bool Flat;
    public bool ThrowingCap;
    public bool Seeker;
    public int ScenarioNum;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = NameSize)]
    public string Stage;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = NameSize)]
    public string Act;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = NameSize)]
    public string SubAct;
}