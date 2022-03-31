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

server.ClientJoined += (c, _) => {
    c.Metadata["shineSync"] = new ConcurrentBag<int>();
    c.Metadata["loadedSave"] = false;
    c.Metadata["scenario"] = 0;
    c.Metadata["2d"] = false;
    c.Metadata["speedrun"] = false;
    foreach (Client client in server.Clients.Where(client => client.Metadata.ContainsKey("lastGamePacket")).ToArray()) {
        try {
            Task.WaitAll(c.Send((GamePacket) client.Metadata["lastGamePacket"]!, client));
        }
        catch {
            // lol who gives a fuck
        }
    }

    c.PacketTransformer += (_, packet) => {
        if (Settings.Instance.Scenario.MergeEnabled && packet is GamePacket gamePacket) {
            gamePacket.ScenarioNum = (byte?) c.Metadata["scenario"] ?? 200;
            return gamePacket;
        }

        return packet;
    };
};

async Task ClientSyncShineBag(Client client) {
    try {
        if ((bool?) client.Metadata["speedrun"] ?? false) return;
        ConcurrentBag<int> clientBag = (ConcurrentBag<int>) (client.Metadata["shineSync"] ??= new ConcurrentBag<int>());
        foreach (int shine in shineBag.Except(clientBag).ToArray()) {
            clientBag.Add(shine);
            await client.Send(new ShinePacket {
                ShineId = shine
            });
        }
    }
    catch {
        // errors that can happen when sending will crash the server :)
    }
}

async void SyncShineBag() {
    try {
        await Parallel.ForEachAsync(server.Clients.ToArray(), async (client, _) => await ClientSyncShineBag(client));
    }
    catch {
        // errors that can happen shines change will crash the server :)
    }
}

Timer timer = new Timer(120000);
timer.AutoReset = true;
timer.Enabled = true;
timer.Elapsed += (_, _) => { SyncShineBag(); };
timer.Start();

float MarioSize(bool is2d) {
    return is2d ? 180 : 160;
}

server.PacketHandler = (c, p) => {
    switch (p) {
        case GamePacket gamePacket: {
            c.Logger.Info($"Got game packet {gamePacket.Stage}->{gamePacket.ScenarioNum}");
            c.Metadata["scenario"] = gamePacket.ScenarioNum;
            c.Metadata["2d"] = gamePacket.Is2d;
            c.Metadata["lastGamePacket"] = gamePacket;
            switch (gamePacket.Stage) {
                case "CapWorldHomeStage" when gamePacket.ScenarioNum == 0:
                    c.Metadata["speedrun"] = true;
                    ConcurrentBag<int> clientBag = (ConcurrentBag<int>) (c.Metadata["shineSync"] ??= new ConcurrentBag<int>());
                    clientBag.Clear();
                    break;
                case "WaterfallWorldHomeStage" when gamePacket.ScenarioNum > 0:
                    c.Metadata["speedrun"] = false;
                    ClientSyncShineBag(c);
                    break;
            }
            break;
        }
        case TagPacket tagPacket: {
            if ((tagPacket.UpdateType & TagPacket.TagUpdate.State) != 0) c.Metadata["seeking"] = tagPacket.IsIt;
            if ((tagPacket.UpdateType & TagPacket.TagUpdate.Time) != 0) c.Metadata["time"] = new Time(tagPacket.Minutes, tagPacket.Seconds, DateTime.Now);
            break;
        }
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
        case PlayerPacket playerPacket when Settings.Instance.Flip.Enabled
                                            && Settings.Instance.Flip.Pov is FlipOptions.Both or FlipOptions.Others
                                            && Settings.Instance.Flip.Players.Contains(c.Id): {
            playerPacket.Position += Vector3.UnitY * MarioSize((bool) c.Metadata["2d"]);
            playerPacket.Rotation *= Quaternion.CreateFromRotationMatrix(Matrix4x4.CreateRotationX(MathF.PI)) * Quaternion.CreateFromRotationMatrix(Matrix4x4.CreateRotationY(MathF.PI));
            server.Broadcast(playerPacket, c);
            return false;
        }
        case PlayerPacket playerPacket when Settings.Instance.Flip.Enabled
                                            && Settings.Instance.Flip.Pov is FlipOptions.Both or FlipOptions.Self
                                            && !Settings.Instance.Flip.Players.Contains(c.Id): {
            server.BroadcastReplace(playerPacket, c, (from, to, sp) => {
                if (Settings.Instance.Flip.Players.Contains(to.Id)) {
                    sp.Position += Vector3.UnitY * MarioSize((bool) c.Metadata["2d"]);
                    sp.Rotation *= Quaternion.CreateFromRotationMatrix(Matrix4x4.CreateRotationX(MathF.PI)) * Quaternion.CreateFromRotationMatrix(Matrix4x4.CreateRotationY(MathF.PI));
                }

                to.Send(sp, from);
            });
            return false;
        }
    }

    return true;
};

CommandHandler.RegisterCommand("send", args => {
    const string optionUsage = "Usage: send <stage> <id> <scenario[0..255]> <player/*>";
    if (args.Length < 4)
        return optionUsage;

    string stage = args[0];
    string id = args[1];

    if (Constants.MapNames.TryGetValue(stage, out string? mapName)) {
        stage = mapName;
    }

    if (!sbyte.TryParse(args[2], out sbyte scenario)) return $"Invalid scenario number {args[2]} (range: [-128 to 127])";
    Client[] players = args[3] == "*" ? server.Clients.Where(c => c.Connected).ToArray() : server.Clients.Where(c => c.Connected && args[3..].Contains(c.Name)).ToArray();
    Parallel.ForEachAsync(players, async (c, _) => {
        await c.Send(new ChangeStagePacket {
            Stage = stage,
            Id = id,
            Scenario = scenario,
            SubScenarioType = 0
        });
    }).Wait();
    return $"Sent players to {stage}:{scenario}";
});

CommandHandler.RegisterCommand("sendall", args => {
    const string optionUsage = "Usage: sendall <stage>";
    if (args.Length < 1)
        return optionUsage;

    string stage = args[0];

    if (Constants.MapNames.TryGetValue(stage, out string? mapName)) {
        stage = mapName;
    }

    Client[] players = server.Clients.Where(c => c.Connected).ToArray();

    Parallel.ForEachAsync(players, async (c, _) => {
        await c.Send(new ChangeStagePacket {
            Stage = stage,
            Id = "",
            Scenario = -1,
            SubScenarioType = 0
        });
    }).Wait();

    return $"Sent players to {stage}:{-1}";
});

CommandHandler.RegisterCommand("scenario", args => {
    const string optionUsage = "Valid options: merge [true/false]";
    if (args.Length < 1)
        return optionUsage;
    switch (args[0]) {
        case "merge" when args.Length == 2: {
            if (bool.TryParse(args[1], out bool result)) {
                Settings.Instance.Scenario.MergeEnabled = result;
                Settings.SaveSettings();
                return result ? "Enabled scenario merge" : "Disabled scenario merge";
            }

            return optionUsage;
        }
        case "merge" when args.Length == 1: {
            return $"Scenario merging is {(Settings.Instance.Scenario.MergeEnabled)}";
        }
        default:
            return optionUsage;
    }
});

CommandHandler.RegisterCommand("tag", args => {
    const string optionUsage = "Valid options:\n\ttime <user/*> <minutes[0-65535]> <seconds[0-59]>\n\tseeking <user/*> <true/false>\n\tstart <time> <seekers>";
    if (args.Length < 3)
        return optionUsage;
    switch (args[0]) {
        case "time" when args.Length == 4: {
            if (args[1] != "*" && server.Clients.All(x => x.Name != args[1])) return $"Cannot find user {args[1]}";
            Client? client = server.Clients.FirstOrDefault(x => x.Name == args[1]);
            if (!ushort.TryParse(args[2], out ushort minutes)) return $"Invalid time for minutes {args[2]} (range: 0-65535)";
            if (!byte.TryParse(args[3], out byte seconds) || seconds >= 60) return $"Invalid time for seconds {args[3]} (range: 0-59)";
            TagPacket tagPacket = new TagPacket {
                UpdateType = TagPacket.TagUpdate.Time,
                Minutes = minutes,
                Seconds = seconds
            };
            if (args[1] == "*")
                server.Broadcast(tagPacket);
            else
                client?.Send(tagPacket);
            return $"Set time for {(args[1] == "*" ? "everyone" : args[1])} to {minutes}:{seconds}";
        }
        case "seeking" when args.Length == 3: {
            if (args[1] != "*" && server.Clients.All(x => x.Name != args[1])) return $"Cannot find user {args[1]}";
            Client? client = server.Clients.FirstOrDefault(x => x.Name == args[1]);
            if (!bool.TryParse(args[2], out bool seeking)) return $"Usage: tag seeking {args[1]} <true/false>";
            TagPacket tagPacket = new TagPacket {
                UpdateType = TagPacket.TagUpdate.State,
                IsIt = seeking
            };
            if (args[1] == "*")
                server.Broadcast(tagPacket);
            else
                client?.Send(tagPacket);
            return $"Set {(args[1] == "*" ? "everyone" : args[1])} to {(seeking ? "seeker" : "hider")}";
        }
        case "start" when args.Length > 2: {
            if (!byte.TryParse(args[1], out byte time)) return $"Invalid countdown seconds {args[1]} (range: 0-255)";
            string[] seekerNames = args[2..];
            Client[] seekers = server.Clients.Where(c => seekerNames.Contains(c.Name)).ToArray();
            if (seekers.Length != seekerNames.Length)
                return $"Couldn't find seeker{(seekerNames.Length > 1 ? "s" : "")}: {string.Join(", ", seekerNames.Where(name => server.Clients.All(c => c.Name != name)))}";
            Task.Run(async () => {
                int realTime = 1000 * time;
                await Task.Delay(realTime);
                await Task.WhenAll(
                    Parallel.ForEachAsync(seekers, async (seeker, _) =>
                        await seeker.Send(new TagPacket {
                            UpdateType = TagPacket.TagUpdate.State,
                            IsIt = true
                        })),
                    Parallel.ForEachAsync(server.Clients.Except(seekers), async (hider, _) =>
                        await hider.Send(new TagPacket {
                            UpdateType = TagPacket.TagUpdate.State,
                            IsIt = false
                        })
                    )
                );
                consoleLogger.Info($"Started game with seekers {string.Join(", ", seekerNames)}");
            });
            return $"Starting game in {time} seconds with seekers {string.Join(", ", seekerNames)}";
        }
        default:
            return optionUsage;
    }
});

CommandHandler.RegisterCommand("list", _ => $"List: {string.Join("\n\t", server.Clients.Select(x => $"{x.Name} ({x.Id})"))}");

CommandHandler.RegisterCommand("flip", args => {
    const string optionUsage = "Valid options: \n\tlist\n\tadd <user id>\n\tremove <user id>\n\tset <true/false>\n\tpov <both/self/others>";
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
                Settings.Instance.Flip.Enabled = result;
                Settings.SaveSettings();
                return result ? "Enabled player flipping" : "Disabled player flipping";
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
    const string optionUsage = "Valid options: list, clear, sync, send";
    if (args.Length < 1)
        return optionUsage;
    switch (args[0]) {
        case "list" when args.Length == 1:
            return $"Shines: {string.Join(", ", shineBag)}";
        case "clear" when args.Length == 1:
            shineBag.Clear();
            foreach (ConcurrentBag<int> playerBag in server.Clients.Select(serverClient => (ConcurrentBag<int>) serverClient.Metadata["shineSync"])) playerBag.Clear();

            return "Cleared shine bags";
        case "sync" when args.Length == 1:
            SyncShineBag();
            return "Synced shine bag automatically";
        case "send" when args.Length >= 3:
            if(int.TryParse(args[1], out int id)) {
                Client[] players = args[2] == "*" ? server.Clients.Where(c => c.Connected).ToArray() : server.Clients.Where(c => c.Connected && args[3..].Contains(c.Name)).ToArray();
                Parallel.ForEachAsync(players, async (c,_) => {
                    await c.Send(new ShinePacket {
                        ShineId = id
                    });
                }).Wait();
                return $"Sent Shine Num {id}";
            }

            return optionUsage;
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
        if (text != null) {
            foreach (string returnString in CommandHandler.GetResult(text).ReturnStrings) {
                consoleLogger.Info(returnString);
            }
        }
    }
});

await listenTask;