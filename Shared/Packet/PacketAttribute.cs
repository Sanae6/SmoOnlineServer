namespace Shared.Packet; 

[AttributeUsage(AttributeTargets.Struct)]
public class PacketAttribute : Attribute {
    public PacketType Type { get; }
    public PacketAttribute(PacketType type) {
        Type = type;
    }
}