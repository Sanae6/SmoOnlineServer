using System.Buffers;
using System.Net.Sockets;
using System.Numerics;
using System.Runtime.InteropServices;
using Shared;
using Shared.Packet;
using Shared.Packet.Packets;

// Guid ownId = new Guid(1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
Guid baseOtherId = Guid.Parse("8ca3fcdd-2940-1000-b5f8-579301fcbfbb");

PacketType[] reboundPackets = {
    PacketType.Player,
    PacketType.Cap,
    PacketType.Capture,
    PacketType.Costume,
    PacketType.Tag,
    PacketType.Game,
    // PacketType.Shine
};

string lastCapture = "";

async Task S(string n, Guid otherId, Guid ownId) {
    Logger logger = new Logger($"Client ({n})");
    TcpClient client = new TcpClient(args[0], 1027);
    NetworkStream stream = client.GetStream();
    logger.Info("Connected!");
    async Task<bool> Read(Memory<byte> readMem, int readSize, int readOffset) {
        readSize += readOffset;
        while (readOffset < readSize) {
            int size = await stream.ReadAsync(readMem[readOffset..readSize]);
            if (size == 0) {
                // treat it as a disconnect and exit
                logger.Info($"Socket {client.Client.RemoteEndPoint} disconnected.");
                return false;
            }

            readOffset += size;
        }

        return true;
    }

    {
        ConnectPacket connect = new ConnectPacket {
            ConnectionType = ConnectPacket.ConnectionTypes.FirstConnection,
            ClientName = n
        };
        PacketHeader coolHeader = new PacketHeader {
            Type = PacketType.Connect,
            Id = ownId,
            PacketSize = connect.Size,
        };
        IMemoryOwner<byte> connectOwner = MemoryPool<byte>.Shared.RentZero(Constants.HeaderSize + connect.Size);
        // coolHeader.Serialize(connectOwner.Memory.Span[..Constants.HeaderSize]);
        MemoryMarshal.Write(connectOwner.Memory.Span[..Constants.HeaderSize], ref coolHeader);
        connect.Serialize(connectOwner.Memory.Span[Constants.HeaderSize..(Constants.HeaderSize + connect.Size)]);
        await stream.WriteAsync(connectOwner.Memory[..(Constants.HeaderSize + connect.Size)]);
        connectOwner.Dispose();
    }
    
    while (true) {
        IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.RentZero(0xFF);
        if (!await Read(owner.Memory, Constants.HeaderSize, 0)) return;
        PacketHeader header = MemoryMarshal.Read<PacketHeader>(owner.Memory.Span);
        if (header.Size > 0) {
            if (!await Read(owner.Memory, header.PacketSize, Constants.HeaderSize)) return;
        }
        PacketType type = header.Type;
        if (header.Id != otherId) continue;
        if (reboundPackets.All(x => x != type)) continue;
        if (type == PacketType.Player) {
            
        }
        header.Id = ownId;
        MemoryMarshal.Write(owner.Memory.Span[..Constants.HeaderSize], ref header);
        await stream.WriteAsync(owner.Memory[..(Constants.HeaderSize + header.PacketSize)]);
        owner.Dispose();
    }
}

Guid temp = baseOtherId;
IEnumerable<Task> stuff = Enumerable.Range(0, 7).Select(i => {
    Guid newOwnId = Guid.NewGuid();
    Task task = S($"Sussy {i}", temp, newOwnId);
    temp = newOwnId;
    return task;
});
await Task.WhenAll(stuff.ToArray());