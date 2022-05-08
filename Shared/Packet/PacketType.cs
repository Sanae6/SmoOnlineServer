namespace Shared.Packet;

public enum PacketType : short {
    Unknown,
    Init,
    Player,
    Cap,
    Game,
    Tag,
    Connect,
    Disconnect,
    Costume,
    Shine,
    Capture,
    ChangeStage,
    Command
}