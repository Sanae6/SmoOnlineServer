using System.Reflection;
using System.Runtime.InteropServices;
using Shared.Packet;
using Shared.Packet.Packets;

namespace Shared;

public static class Constants {
    public const int CostumeNameSize = 0x20;

    // dictionary of packet types to packet
    public static readonly Dictionary<Type, PacketAttribute> PacketMap = Assembly
        .GetExecutingAssembly()
        .GetTypes()
        .Where(type => type.IsAssignableTo(typeof(IPacket)))
        .ToDictionary(type => type, type => type.GetCustomAttribute<PacketAttribute>()!);
    public static readonly Dictionary<PacketType, Type> PacketIdMap = Assembly
        .GetExecutingAssembly()
        .GetTypes()
        .Where(type => type.IsAssignableTo(typeof(IPacket)) && type.GetCustomAttribute<PacketAttribute>() != null)
        .ToDictionary(type => type.GetCustomAttribute<PacketAttribute>()!.Type, type => type);

    public static int HeaderSize { get; } = Marshal.SizeOf<PacketHeader>();

    public static readonly Dictionary<string, string> MapNames = new Dictionary<string, string>() {
        {"Cap", "CapWorldHomeStage"},
        {"Cascade", "WaterfallWorldHomeStage"},
        {"Sand", "SandWorldHomeStage"},
        {"Lake", "LakeWorldHomeStage"},
        {"Wooded", "ForestWorldHomeStage"},
        {"Cloud", "CloudWorldHomeStage"},
        {"Lost", "ClashWorldHomeStage"},
        {"Metro", "CityWorldHomeStage"},
        {"Sea", "SeaWorldHomeStage"},
        {"Snow", "SnowWorldHomeStage"},
        {"Lunch", "LavaWorldHomeStage"},
        {"Ruined", "BossRaidWorldHomeStage"},
        {"Bowser", "SkyWorldHomeStage"},
        {"Moon", "MoonWorldHomeStage"},
        {"Mush", "PeachWorldHomeStage"}
    };
}