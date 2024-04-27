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
    public bool Ignored = false;
    public bool Banned = false;
    public CostumePacket? CurrentCostume = null; // required for proper client sync
    public string Name {
        get => Logger.Name;
        set => Logger.Name = value;
    }

    public Guid Id;
    public Socket? Socket;
    public Server Server { get; init; } = null!; //init'd in object initializer
    public Logger Logger { get; }

    public Client(Socket socket) {
        Socket = socket;
        Logger = new Logger("Unknown User");
    }

    // copy Client to use existing data for a new reconnected connection with a new socket
    public Client(Client other, Socket socket) {
        Metadata       = other.Metadata;
        Connected      = other.Connected;
        CurrentCostume = other.CurrentCostume;
        Id             = other.Id;
        Socket         = socket;
        Server         = other.Server;
        Logger         = other.Logger;
    }

    public void Dispose() {
        if (Socket?.Connected is true) {
            Socket.Disconnect(false);
        }
    }


    public async Task Send<T>(T packet, Client? sender = null) where T : struct, IPacket {
        IMemoryOwner<byte> memory = MemoryPool<byte>.Shared.RentZero(Constants.HeaderSize + packet.Size);

        PacketAttribute packetAttribute = Constants.PacketMap[typeof(T)];
        try {
            // don't send most packets to ignored players
            if (Ignored && packetAttribute.Type != PacketType.Init && packetAttribute.Type != PacketType.ChangeStage) {
                memory.Dispose();
                return;
            }
            Server.FillPacket(new PacketHeader {
                Id         = sender?.Id ?? Id,
                Type       = packetAttribute.Type,
                PacketSize = packet.Size
            }, packet, memory.Memory);
        }
        catch (Exception e) {
            Logger.Error($"Failed to serialize {packetAttribute.Type}");
            Logger.Error(e);
        }

        await Socket!.SendAsync(memory.Memory[..(Constants.HeaderSize + packet.Size)], SocketFlags.None);
        memory.Dispose();
    }

    public async Task Send(Memory<byte> data, Client? sender) {
        PacketHeader header = new PacketHeader();
        header.Deserialize(data.Span);

        if (!Connected && !Ignored && header.Type != PacketType.Connect) {
            Server.Logger.Error($"Didn't send {header.Type} to {Id} because they weren't connected yet");
            return;
        }

        // don't send most packets to ignored players
        if (Ignored && header.Type != PacketType.Init && header.Type != PacketType.ChangeStage) {
            return;
        }

        await Socket!.SendAsync(data[..(Constants.HeaderSize + header.PacketSize)], SocketFlags.None);
    }

    public void CleanMetadataOnNewConnection() {
        object? tmp;
        Metadata.TryRemove("time",              out tmp);
        Metadata.TryRemove("seeking",           out tmp);
        Metadata.TryRemove("lastCostumePacket", out tmp);
        Metadata.TryRemove("lastCapturePacket", out tmp);
        Metadata.TryRemove("lastGamePacket",    out tmp);
        Metadata.TryRemove("lastPlayerPacket",  out tmp);
    }

    public TagPacket? GetTagPacket() {
        var time = (Time?) (this.Metadata.ContainsKey("time")    ? this.Metadata["time"]    : null);
        var seek = (bool?) (this.Metadata.ContainsKey("seeking") ? this.Metadata["seeking"] : null);
        if (time == null && seek == null) { return null; }
        return new TagPacket {
            UpdateType = (seek != null ? TagPacket.TagUpdate.State : 0) | (time != null ? TagPacket.TagUpdate.Time: 0),
            IsIt       = seek ?? false,
            Seconds    = (byte)   (time?.Seconds ?? 0),
            Minutes    = (ushort) (time?.Minutes ?? 0),
        };
    }

    public static bool operator ==(Client? left, Client? right) {
        return left is { } leftClient && right is { } rightClient && leftClient.Id == rightClient.Id;
    }

    public static bool operator !=(Client? left, Client? right) {
        return !(left == right);
    }

    public override bool Equals(object? obj) {
        if (obj is Client)
            return this == (Client)obj;
        else
            return false;
    }

    public override int GetHashCode() {
        return Id.GetHashCode(); //relies upon same info as == operator.
    }
}
