using System.Collections.Concurrent;
using Server;
using Shared.Packet.Packets;

Server.Server server = new Server.Server();
ConcurrentBag<int> shineBag = new ConcurrentBag<int>();

server.ClientJoined += async (c, type) => {
    c.Metadata["shineSync"] = new ConcurrentBag<int>();
    c.Metadata["loadedSave"] = false;
};

async Task ClientSyncShineBag(Client client) {
    foreach (int shine in shineBag.Except((ConcurrentBag<int>) client.Metadata["shineSync"]))
        await client.Send(new ShinePacket {
            ShineId = shine
        });
}

async void SyncShineBag() {
    await Parallel.ForEachAsync(server.Clients, async (client, _) => {
        await ClientSyncShineBag(client);
    });
}

server.PacketHandler += async (c, p) => {
    switch (p) {
        case CostumePacket:
            await ClientSyncShineBag(c);
            c.Metadata["loadedSave"] = true;
            break;
        case ShinePacket shinePacket: {
            if (c.Metadata["loadedSave"] is false) return;
            ConcurrentBag<int> playerBag = (ConcurrentBag<int>) c.Metadata["shineSync"];
            shineBag.Add(shinePacket.ShineId);
            playerBag.Add(shinePacket.ShineId);
            SyncShineBag();
            break;
        }
    }
};

await server.Listen(1027);