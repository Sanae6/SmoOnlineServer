using System.Runtime.InteropServices;

namespace Shared.Packet.Packets;

[Packet(PacketType.Init)]
public struct InitPacket : IPacket {
    public short Size { get; } = 2;
    public ushort MaxPlayers = 0;

    public InitPacket() { }

    public void Serialize(Span<byte> data) {
        MemoryMarshal.Write(data, ref MaxPlayers);
    }

    public void Deserialize(ReadOnlySpan<byte> data) {
        MaxPlayers = MemoryMarshal.Read<ushort>(data);
    }
}