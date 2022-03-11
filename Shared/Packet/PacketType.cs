namespace Shared.Packet;

public enum PacketType : short {
    Unknown,
    Player,
    Cap,
    Game,
    Tag,
    Connect,
    Disconnect,
    Costume,
    Shine,
    Capture,
    Command
}