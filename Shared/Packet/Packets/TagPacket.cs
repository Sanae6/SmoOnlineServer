using System.Runtime.InteropServices;

namespace Shared.Packet.Packets;

[Packet(PacketType.Tag)]
public struct TagPacket : IPacket {
    public GameMode GameMode;
    public TagUpdate UpdateType;
    public bool IsIt;
    public byte Seconds;
    public ushort Minutes;

    public short Size => 5;

    public void Serialize(Span<byte> data) {
        byte both = (byte)((byte) UpdateType | ((byte) GameMode << 4));
        MemoryMarshal.Write(data,      ref both);
        MemoryMarshal.Write(data[1..], ref IsIt);
        MemoryMarshal.Write(data[2..], ref Seconds);
        MemoryMarshal.Write(data[3..], ref Minutes);
    }

    public void Deserialize(ReadOnlySpan<byte> data) {
        byte both  = MemoryMarshal.Read<byte>(data);
        GameMode   = (GameMode) (sbyte) (((((both & (byte) 0xf0) >> 4) + 1) % 16) - 1);
        UpdateType = (TagUpdate) (byte) (both & (byte) 0x0f);
        IsIt       = MemoryMarshal.Read<bool>(data[1..]);
        Seconds    = MemoryMarshal.Read<byte>(data[2..]);
        Minutes    = MemoryMarshal.Read<ushort>(data[3..]);
    }

    [Flags]
    public enum TagUpdate : byte {
        None      =  0,
        Time      =  1,
        State     =  2,
        Both      =  3,
        Unknown04 =  4,
        Unknown05 =  5,
        Unknown06 =  6,
        Unknown07 =  7,
        Unknown08 =  8,
        Unknown09 =  9,
        Unknown10 = 10,
        Unknown11 = 11,
        Unknown12 = 12,
        Unknown13 = 13,
        Unknown14 = 14,
        All       = 15,
    }
}

public enum GameMode : sbyte {
    None        = -1,
    Legacy      =  0,
    HideAndSeek =  1,
    Sardines    =  2,
    FreezeTag   =  3,
    Unknown04   =  4,
    Unknown05   =  5,
    Unknown06   =  6,
    Unknown07   =  7,
    Unknown08   =  8,
    Unknown09   =  9,
    Unknown10   = 10,
    Unknown11   = 11,
    Unknown12   = 12,
    Unknown13   = 13,
    Reserved    = 14, // extension possibility for more future game modes with an extra added byte
}
