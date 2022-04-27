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
        .Where(type => type.IsAssignableTo(typeof(IPacket)) && type.GetCustomAttribute<PacketAttribute>() != null)
        .ToDictionary(type => type, type => type.GetCustomAttribute<PacketAttribute>()!);
    public static readonly Dictionary<PacketType, Type> PacketIdMap = Assembly
        .GetExecutingAssembly()
        .GetTypes()
        .Where(type => type.IsAssignableTo(typeof(IPacket)) && type.GetCustomAttribute<PacketAttribute>() != null)
        .ToDictionary(type => type.GetCustomAttribute<PacketAttribute>()!.Type, type => type);

    public static int HeaderSize { get; } = PacketHeader.StaticSize;

    public static readonly Dictionary<string, string> MapNames = new Dictionary<string, string>() {
        {"cap", "CapWorldHomeStage"},
        {"cascade", "WaterfallWorldHomeStage"},
        {"sand", "SandWorldHomeStage"},
        {"lake", "LakeWorldHomeStage"},
        {"wooded", "ForestWorldHomeStage"},
        {"cloud", "CloudWorldHomeStage"},
        {"lost", "ClashWorldHomeStage"},
        {"metro", "CityWorldHomeStage"},
        {"sea", "SeaWorldHomeStage"},
        {"snow", "SnowWorldHomeStage"},
        {"lunch", "LavaWorldHomeStage"},
        {"ruined", "BossRaidWorldHomeStage"},
        {"bowser", "SkyWorldHomeStage"},
        {"moon", "MoonWorldHomeStage"},
        {"mush", "PeachWorldHomeStage"},
        {"dark", "Special1WorldHomeStage"},
        {"darker", "Special2WorldHomeStage"}
    };
}