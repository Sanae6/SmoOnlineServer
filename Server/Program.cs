using System.Collections.Concurrent;
using System.Numerics;
using Server;
using Shared;
using Shared.Packet.Packets;
using Timer = System.Timers.Timer;

Server.Server server = new Server.Server();
HashSet<int> shineBag = new HashSet<int>();
// int shineTx = 0; // used for logging
CancellationTokenSource cts = new CancellationTokenSource();
Task listenTask = server.Listen(cts.Token);
Logger consoleLogger = new Logger("Console");

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
    await Parallel.ForEachAsync(server.Clients, async (client, _) => { await ClientSyncShineBag(client); });
}

Timer timer = new Timer(120000);
timer.AutoReset = true;
timer.Enabled = true;
timer.Elapsed += (_, _) => { SyncShineBag(); };
timer.Start();
bool flipEnabled = Settings.Instance.Flip.EnabledOnStart;

float MarioSize(bool is2d) => is2d ? 180 : 160;
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
        case PlayerPacket playerPacket when flipEnabled && Settings.Instance.Flip.Pov is FlipOptions.Both or FlipOptions.Others && Settings.Instance.Flip.Players.Contains(c.Id): {
            playerPacket.Position += Vector3.UnitY * MarioSize(playerPacket.Is2d);
            playerPacket.Rotation *= Quaternion.CreateFromRotationMatrix(Matrix4x4.CreateRotationX(MathF.PI)) * Quaternion.CreateFromRotationMatrix(Matrix4x4.CreateRotationY(MathF.PI));
            server.Broadcast(playerPacket, c);
            return false;
        }
        case PlayerPacket playerPacket when flipEnabled && Settings.Instance.Flip.Pov is FlipOptions.Both or FlipOptions.Self && !Settings.Instance.Flip.Players.Contains(c.Id): {
            server.BroadcastReplace(playerPacket, c, (from, to, sp) => {
                if (Settings.Instance.Flip.Players.Contains(to.Id)) {
                    sp.Position += Vector3.UnitY * MarioSize(playerPacket.Is2d);
                    sp.Rotation *= Quaternion.CreateFromRotationMatrix(Matrix4x4.CreateRotationX(MathF.PI)) * Quaternion.CreateFromRotationMatrix(Matrix4x4.CreateRotationY(MathF.PI));
                }

                to.Send(sp, from);
            });
            return false;
        }
    }

    return true;
};

CommandHandler.RegisterCommand("flip", args => {
    const string optionUsage = "Valid options: list, add <user id>, remove <user id>, set <true/false>, pov <both/self/others>";
    if (args.Length < 1)
        return optionUsage;
    switch (args[0]) {
        case "list" when args.Length == 1:
            return "User ids: " + string.Join(", ", Settings.Instance.Flip.Players.ToList());
        case "add" when args.Length == 2: {
            if (Guid.TryParse(args[1], out Guid result)) {
                Settings.Instance.Flip.Players.Add(result);
                Settings.SaveSettings();
                return $"Added {result} to flipped players";
            }

            return $"Invalid user id {args[1]}";
        }
        case "remove" when args.Length == 2: {
            if (Guid.TryParse(args[1], out Guid result)) {
                string output = Settings.Instance.Flip.Players.Remove(result) ? $"Removed {result} to flipped players" : $"User {result} wasn't in the flipped players list";
                Settings.SaveSettings();
                return output;
            }

            return $"Invalid user id {args[1]}";
        }
        case "set" when args.Length == 2: {
            if (bool.TryParse(args[1], out bool result)) {
                flipEnabled = result;
                return result ? "Enabled player flipping for session" : "Disabled player flipping for session";
            }

            return optionUsage;
        }
        case "pov" when args.Length == 2: {
            if (Enum.TryParse(args[1], true, out FlipOptions result)) {
                Settings.Instance.Flip.Pov = result;
                Settings.SaveSettings();
                return $"Point of view set to {result}";
            }

            return optionUsage;
        }
        default:
            return optionUsage;
    }
});

CommandHandler.RegisterCommand("shine", args => {
    const string optionUsage = "Valid options: list";
    if (args.Length < 1)
        return optionUsage;
    switch (args[0]) {
        case "list" when args.Length == 1:
            return $"Shines: {string.Join(", ", shineBag)}";
        default:
            return optionUsage;
    }
});

CommandHandler.RegisterCommand("loadsettings", _ => {
    Settings.LoadSettings();
    return "Loaded settings.json";
});

CommandHandler.RegisterCommand("savesettings", _ => {
    Settings.SaveSettings();
    return "Saved settings.json";
});

Console.CancelKeyPress += (_, e) => {
    e.Cancel = true;
    consoleLogger.Info("Received Ctrl+C");
    cts.Cancel();
};

CommandHandler.RegisterCommand("exit", _ => {
    cts.Cancel();
    return "Shutting down clients";
});

CommandHandler.RegisterCommand("quit", _ => {
    cts.Cancel();
    return "Shutting down clients";
});

Task.Run(() => {
    consoleLogger.Info("Run help command for valid commands.");
    while (true) {
        string? text = Console.ReadLine();
        if (text != null) consoleLogger.Info(CommandHandler.GetResult(text));
    }
});

await listenTask;