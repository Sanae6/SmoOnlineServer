using System.Reflection;
using System.Runtime.InteropServices;
using Shared.Packet;
using Shared.Packet.Packets;

namespace Shared; 

public static class Constants {
    public const int MaxPacketSize = 256;
    public const int MaxClients = 4;
    public static int HeaderSize => Marshal.SizeOf<PacketHeader>();
    
    // dictionary of packet types to packet
    public static readonly Dictionary<Type, PacketAttribute> Packets = Assembly
        .GetExecutingAssembly()
        .GetTypes()
        .Where(type => type.IsAssignableTo(typeof(IPacket)))
        .ToDictionary(type => type, type => type.GetCustomAttribute<PacketAttribute>()!);
}