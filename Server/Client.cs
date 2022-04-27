using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Shared;
using Shared.Packet;
using Shared.Packet.Packets;

namespace Server;

public class Client : IDisposable {
    public readonly ConcurrentDictionary<string, object?> Metadata = new ConcurrentDictionary<string, object?>(); // can be used to store any information about a player
    public bool Connected = false;
    public CostumePacket? CurrentCostume = null; // required for proper client sync
    public string Name {
        get => Logger.Name;
        set => Logger.Name = value;
    }

    public Guid Id;
    public Socket? Socket;
    public Server Server { get; init; }
    public Logger Logger { get; }

    public Client(Socket socket) {
        Socket = socket;
        Logger = new Logger("Unknown User");
    }

    public void Dispose() {
        if (Socket?.Connected is true)
            Socket.Disconnect(false);
    }


    public async Task Send<T>(T packet, Client? sender = null) where T : struct, IPacket {
        IMemoryOwner<byte> memory = MemoryPool<byte>.Shared.RentZero(Constants.HeaderSize + packet.Size);

        PacketAttribute packetAttribute = Constants.PacketMap[typeof(T)];
        if (packetAttribute.Type is not PacketType.Cap and not PacketType.Player)
            Logger.Info($"About to receive {packetAttribute.Type} ({(short)packetAttribute.Type}) - {typeof(T)}");
        PacketHeader header = new PacketHeader {
            Id = sender?.Id ?? Id,
            Type = packetAttribute.Type,
            PacketSize = packet.Size
        };
        Server.FillPacket(header, packet, memory.Memory);
        await Send(memory.Memory, sender, packetAttribute.Type);
        memory.Dispose();
    }

    public async Task Send(Memory<byte> data, Client? sender, PacketType? partTime = null) {
        PacketHeader header = new PacketHeader();
        header.Deserialize(data.Span);
        if (!Connected && header.Type is not PacketType.Connect) {
            Server.Logger.Error($"Didn't send {header.Type} to {Id} because they weren't connected yet");
            return;
        }

        if (header.Type is not PacketType.Cap and not PacketType.Player) {
            Logger.Info($"About to receive {header.Type} + ({(short)header.Type}), {partTime} ({(short?) partTime}) {new StackTrace().ToString()}");
        }

        await Socket!.SendAsync(data[..(Constants.HeaderSize + header.PacketSize)], SocketFlags.None);
    }

    public static bool operator ==(Client? left, Client? right) {
        return left is { } leftClient && right is { } rightClient && leftClient.Id == rightClient.Id;
    }

    public static bool operator !=(Client? left, Client? right) {
        return !(left == right);
    }
}