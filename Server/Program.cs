using System.Collections.Concurrent;
using System.Numerics;
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
bool piss = false;

// Guid lycel = Guid.Parse("d5feae62-2e71-1000-88fd-597ea147ae88");
Guid lycel = Guid.Parse("5e1f9db4-1c27-1000-a421-4701972e443e");

server.PacketHandler = (c, p) => {
    switch (p) {
        case CostumePacket:
            ClientSyncShineBag(c);
            c.Metadata["loadedSave"] = true;
            break;
        case ShinePacket shinePacket: {
            if (c.Metadata["loadedSave"] is false) break;
            ConcurrentBag<int> playerBag = (ConcurrentBag<int>) c.Metadata["shineSync"];
            shineBag.Add(shinePacket.ShineId);
            if (playerBag.Contains(shinePacket.ShineId)) break;
            c.Logger.Info($"Got shine {shinePacket.ShineId}");
            playerBag.Add(shinePacket.ShineId);
            SyncShineBag();
            break;
        }
        case PlayerPacket playerPacket: {
            if (!piss || c.Id != lycel) break;
            playerPacket.Position += Vector3.UnitY * 160;
            playerPacket.Rotation *= Quaternion.CreateFromRotationMatrix(Matrix4x4.CreateRotationX(MathF.PI));
            server.Broadcast(playerPacket, c);
            return false;
        }
    }

    return true;
};

Task.Run(() => {
    while (true) {
        Console.ReadLine();
        piss = !piss;
        server.Logger.Warn($"Lycel flipped to {piss}");
    }
});

await server.Listen(1027);