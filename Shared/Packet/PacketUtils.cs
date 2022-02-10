using System.Runtime.InteropServices;
using Shared.Packet.Packets;

namespace Shared.Packet;

public static class PacketUtils {
    public static void SerializeHeaded<T>(Span<byte> data, PacketHeader header, T t) where T : struct, IPacket {
        header.Serialize(data);
        t.Serialize(data[Constants.HeaderSize..]);
    }

    public static T Deserialize<T>(Span<byte> data) where T : IPacket, new() {
        T packet = new T();
        packet.Deserialize(data);
        return packet;
    }

    public static int SizeOf<T>() where T : struct, IPacket {
        return Constants.HeaderSize + Marshal.SizeOf<T>();
    }
}