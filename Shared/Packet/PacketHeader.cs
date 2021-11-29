using System.Runtime.InteropServices;

namespace Shared.Packet; 

[StructLayout(LayoutKind.Sequential)]
public struct PacketHeader {
    public Guid Id;
    public PacketType Type;
    public PacketSender Sender;
}