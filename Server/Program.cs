using System.Collections.Concurrent;
using Server;
using Shared.Packet.Packets;
using Timer = System.Timers.Timer;

Server.Server server = new Server.Server();
HashSet<int> shineBag = new HashSet<int>();
int shineTx = 0; // used for logging

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

Timer timer = new Timer(120000);
timer.AutoReset = true;
timer.Enabled = true;
timer.Elapsed += (_, _) => {
    SyncShineBag();
};
timer.Start();

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
            if (playerBag.Contains(shinePacket.ShineId)) return;
            c.Logger.Info($"Got shine {shinePacket.ShineId}");
            playerBag.Add(shinePacket.ShineId);
            SyncShineBag();
            break;
        }
    }
};

await server.Listen(1027);