using System.Collections.Concurrent;
using System.Net;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Server;
using Shared;
using Shared.Packet.Packets;
using Timer = System.Timers.Timer;

Server.Server server = new Server.Server();
HashSet<int> shineBag = new HashSet<int>();
CancellationTokenSource cts = new CancellationTokenSource();
bool restartRequested = false;
Logger consoleLogger = new Logger("Console");
DiscordBot bot = new DiscordBot();
await bot.Run();

async Task PersistShines()
{
    if (!Settings.Instance.PersistShines.Enabled)
    {
        return;
    }

    try
    {
        string shineJson = JsonSerializer.Serialize(shineBag);
        await File.WriteAllTextAsync(Settings.Instance.PersistShines.Filename, shineJson);
    }
    catch (Exception ex)
    {
        consoleLogger.Error(ex);
    }
}

async Task LoadShines()
{
    if (!Settings.Instance.PersistShines.Enabled)
    {
        return;
    }

    try
    {
        string shineJson = await File.ReadAllTextAsync(Settings.Instance.PersistShines.Filename);
        var loadedShines = JsonSerializer.Deserialize<HashSet<int>>(shineJson);

        if (loadedShines is not null) shineBag = loadedShines;
    }
    catch (FileNotFoundException)
    {
        // Ignore
    }
    catch (Exception ex)
    {
        consoleLogger.Error(ex);
    }
}

// Load shines table from file
await LoadShines();

server.ClientJoined += (c, _) => {
    c.Metadata["shineSync"] = new ConcurrentBag<int>();
    c.Metadata["loadedSave"] = false;
    c.Metadata["scenario"] = (byte?) 0;
    c.Metadata["2d"] = false;
    c.Metadata["disableShineSync"] = false;
};

async Task ClientSyncShineBag(Client client) {
    if (!Settings.Instance.Shines.Enabled) return;
    try {
        if ((bool?) client.Metadata["disableShineSync"] ?? false) return;
        ConcurrentBag<int> clientBag = (ConcurrentBag<int>) (client.Metadata["shineSync"] ??= new ConcurrentBag<int>());
        foreach (int shine in shineBag.Except(clientBag).Except(Settings.Instance.Shines.Excluded).ToArray()) {
            if (!client.Connected) return;
            await client.Send(new ShinePacket {
                ShineId = shine
            });
            clientBag.Add(shine);
        }
    } catch {
        // errors that can happen when sending will crash the server :)
    }
}

async void SyncShineBag() {
    try {
        await PersistShines();
        await Parallel.ForEachAsync(server.ClientsConnected.ToArray(), async (client, _) => await ClientSyncShineBag(client));
    } catch {
        // errors that can happen shines change will crash the server :)
    }
}

Timer timer = new Timer(120000);
timer.AutoReset = true;
timer.Enabled = true;
timer.Elapsed += (_, _) => { SyncShineBag(); };
timer.Start();

float MarioSize(bool is2d) => is2d ? 180 : 160;

void flipPlayer(Client c, ref PlayerPacket pp) {
    pp.Position += Vector3.UnitY * MarioSize((bool) c.Metadata["2d"]!);
    pp.Rotation *= (
        Quaternion.CreateFromRotationMatrix(Matrix4x4.CreateRotationX(MathF.PI))
      * Quaternion.CreateFromRotationMatrix(Matrix4x4.CreateRotationY(MathF.PI))
    );
};

void logError(Task x) {
    if (x.Exception != null) {
        consoleLogger.Error(x.Exception.ToString());
    }
};

server.PacketHandler = (c, p) => {
    switch (p) {
        case GamePacket gamePacket: {
            // crash ignored player
            if (c.Ignored) {
                c.Logger.Info($"Crashing ignored player after entering stage {gamePacket.Stage}.");
                BanLists.Crash(c, 500);
                return false;
            }

            // crash player entering a banned stage
            if (BanLists.Enabled && BanLists.IsStageBanned(gamePacket.Stage)) {
                c.Logger.Warn($"Crashing player for entering banned stage {gamePacket.Stage}.");
                BanLists.Crash(c, 500);
                return false;
            }

            c.Logger.Info($"Got game packet {gamePacket.Stage}->{gamePacket.ScenarioNum}");

            // reset lastPlayerPacket on stage changes
            object? old = null;
            c.Metadata.TryGetValue("lastGamePacket", out old);
            if (old != null && ((GamePacket) old).Stage != gamePacket.Stage) {
                c.Metadata["lastPlayerPacket"] = null;
            }

            c.Metadata["scenario"] = gamePacket.ScenarioNum;
            c.Metadata["2d"] = gamePacket.Is2d;
            c.Metadata["lastGamePacket"] = gamePacket;

            switch (gamePacket.Stage) {
                case "CapWorldHomeStage"  when gamePacket.ScenarioNum == 1:
                case "CapWorldTowerStage" when gamePacket.ScenarioNum == 1:
                    if (!((bool?) c.Metadata["disableShineSync"] ?? false)) {
                        c.Metadata["disableShineSync"] = true;
                        ((ConcurrentBag<int>) (c.Metadata["shineSync"] ??= new ConcurrentBag<int>())).Clear();
                        c.Logger.Info("Entered Cap on new save, preventing moon sync until Cascade");
                        if (Settings.Instance.Shines.ClearOnNewSaves) {
                            shineBag.Clear();
                            c.Logger.Info("Cleared shine bags");
                            Task.Run(PersistShines);
                        }
                    }
                    break;
                default:
                    if ((bool?) c.Metadata["disableShineSync"] ?? false) {
                        Task.Run(async () => {
                            c.Logger.Info("Entered Cascade or later with moon sync disabled, enabling moon sync again");
                            await Task.Delay(2000);
                            c.Metadata["disableShineSync"] = false;
                            await ClientSyncShineBag(c);
                        });
                    }
                    break;
            }

            if (Settings.Instance.Scenario.MergeEnabled) {
                server.BroadcastReplace(gamePacket, c, (from, to, gp) => {
                    gp.ScenarioNum = (byte?) to.Metadata["scenario"] ?? 200;
#pragma warning disable CS4014
                    to.Send(gp, from).ContinueWith(logError);
#pragma warning restore CS4014
                });
                return false;
            }

            break;
        }

        // ignore all other packets from ignored players
        case IPacket pack when c.Ignored: {
            return false;
        }

        case TagPacket tagPacket: {
            // c.Logger.Info($"Got tag packet: {tagPacket.IsIt}");
            if ((tagPacket.UpdateType & TagPacket.TagUpdate.State) != 0) c.Metadata["seeking"] = tagPacket.IsIt;
            if ((tagPacket.UpdateType & TagPacket.TagUpdate.Time) != 0)
                c.Metadata["time"] = new Time(tagPacket.Minutes, tagPacket.Seconds, DateTime.Now);
            break;
        }

        case CapturePacket capturePacket: {
            // c.Logger.Info($"Got capture packet: {capturePacket.ModelName}");
            c.Metadata["lastCapturePacket"] = capturePacket;
            break;
        }

        case CostumePacket costumePacket: {
            c.Logger.Info($"Got costume packet: {costumePacket.BodyName}, {costumePacket.CapName}");
            c.Metadata["lastCostumePacket"] = costumePacket;
            c.CurrentCostume = costumePacket;
#pragma warning disable CS4014
            ClientSyncShineBag(c); //no point logging since entire def has try/catch
#pragma warning restore CS4014
            c.Metadata["loadedSave"] = true;
            break;
        }

        case ShinePacket shinePacket: {
            if (!Settings.Instance.Shines.Enabled) return false;
            if (Settings.Instance.Shines.Excluded.Contains(shinePacket.ShineId)) {
                c.Logger.Info($"Got moon {shinePacket.ShineId} (excluded)");
                return false;
            }
            if (c.Metadata["loadedSave"] is false) break;
            ConcurrentBag<int> playerBag = (ConcurrentBag<int>)c.Metadata["shineSync"]!;
            shineBag.Add(shinePacket.ShineId);
            if (playerBag.Contains(shinePacket.ShineId)) break;
            c.Logger.Info($"Got moon {shinePacket.ShineId}");
            playerBag.Add(shinePacket.ShineId);
            SyncShineBag();
            break;
        }

        case PlayerPacket playerPacket: {
            c.Metadata["lastPlayerPacket"] = playerPacket;
            // flip for all
            if (   Settings.Instance.Flip.Enabled
                && Settings.Instance.Flip.Pov is FlipOptions.Both or FlipOptions.Others
                && Settings.Instance.Flip.Players.Contains(c.Id)
            ) {
                flipPlayer(c, ref playerPacket);
#pragma warning disable CS4014
                server.Broadcast(playerPacket, c).ContinueWith(logError);
#pragma warning restore CS4014
                return false;
            }
            // flip only for specific clients
            if (   Settings.Instance.Flip.Enabled
                && Settings.Instance.Flip.Pov is FlipOptions.Both or FlipOptions.Self
                && !Settings.Instance.Flip.Players.Contains(c.Id)
            ) {
                server.BroadcastReplace(playerPacket, c, (from, to, sp) => {
                    if (Settings.Instance.Flip.Players.Contains(to.Id)) {
                        flipPlayer(c, ref sp);
                    }
#pragma warning disable CS4014
                    to.Send(sp, from).ContinueWith(logError);
#pragma warning restore CS4014
                });
                return false;
            }
            break;
        }
    }

    return true; // Broadcast packet to all other clients
};

(HashSet<string> failToFind, HashSet<Client> toActUpon, List<(string arg, IEnumerable<string> amb)> ambig) MultiUserCommandHelper(string[] args) {
    HashSet<string> failToFind = new();
    HashSet<Client> toActUpon;
    List<(string arg, IEnumerable<string> amb)> ambig = new();
    if (args[0] == "*") {
        toActUpon = new(server.Clients.Where(c => c.Connected));
    }
    else {
        toActUpon = args[0] == "!*" ? new(server.Clients.Where(c => c.Connected)) : new();
        for (int i = (args[0] == "!*" ? 1 : 0); i < args.Length; i++) {
            string arg = args[i];
            IEnumerable<Client> search = server.Clients.Where(c => c.Connected && (
                c.Name.ToLower().StartsWith(arg.ToLower())
                || (Guid.TryParse(arg, out Guid res) && res == c.Id)
                || (IPAddress.TryParse(arg, out IPAddress? ip) && ip.Equals(((IPEndPoint) c.Socket!.RemoteEndPoint!).Address))
            ));
            if (!search.Any()) {
                failToFind.Add(arg); //none found
            }
            else if (search.Count() > 1) {
                Client? exact = search.FirstOrDefault(x => x.Name == arg);
                if (!ReferenceEquals(exact, null)) {
                    //even though multiple matches, since exact match, it isn't ambiguous
                    if (args[0] == "!*") {
                        toActUpon.Remove(exact);
                    }
                    else {
                        toActUpon.Add(exact);
                    }
                }
                else {
                    if (!ambig.Any(x => x.arg == arg)) {
                        ambig.Add((arg, search.Select(x => x.Name))); //more than one match
                    }
                    foreach (var rem in search.ToList()) { //need copy because can't remove from list while iterating over it
                        toActUpon.Remove(rem);
                    }
                }
            }
            else {
                //only one match, so autocomplete
                if (args[0] == "!*") {
                    toActUpon.Remove(search.First());
                }
                else {
                    toActUpon.Add(search.First());
                }
            }
        }
    }
    return (failToFind, toActUpon, ambig);
}

CommandHandler.RegisterCommand("rejoin", args => {
    if (args.Length == 0) {
        return "Usage: rejoin <* | !* (usernames to not rejoin...) | (usernames to rejoin...)>";
    }

    var res = MultiUserCommandHelper(args);

    StringBuilder sb = new StringBuilder();
    sb.Append(res.toActUpon.Count > 0 ? "Rejoined: " + string.Join(", ", res.toActUpon.Select(x => $"\"{x.Name}\"")) : "");
    sb.Append(res.failToFind.Count > 0 ? "\nFailed to find matches for: " + string.Join(", ", res.failToFind.Select(x => $"\"{x.ToLower()}\"")) : "");
    if (res.ambig.Count > 0) {
        res.ambig.ForEach(x => {
            sb.Append($"\nAmbiguous for \"{x.arg}\": {string.Join(", ", x.amb.Select(x => $"\"{x}\""))}");
        });
    }

    foreach (Client user in res.toActUpon) {
        user.Dispose();
    }

    return sb.ToString();
});

CommandHandler.RegisterCommand("crash", args => {
    if (args.Length == 0) {
        return "Usage: crash <* | !* (usernames to not crash...) | (usernames to crash...)>";
    }

    var res = MultiUserCommandHelper(args);

    StringBuilder sb = new StringBuilder();
    sb.Append(res.toActUpon.Count > 0 ? "Crashed: " + string.Join(", ", res.toActUpon.Select(x => $"\"{x.Name}\"")) : "");
    sb.Append(res.failToFind.Count > 0 ? "\nFailed to find matches for: " + string.Join(", ", res.failToFind.Select(x => $"\"{x.ToLower()}\"")) : "");
    if (res.ambig.Count > 0) {
        res.ambig.ForEach(x => {
            sb.Append($"\nAmbiguous for \"{x.arg}\": {string.Join(", ", x.amb.Select(x => $"\"{x}\""))}");
        });
    }

    foreach (Client user in res.toActUpon) {
        BanLists.Crash(user);
    }

    return sb.ToString();
});

CommandHandler.RegisterCommand("ban",   args => { return BanLists.HandleBanCommand(args, (args) => MultiUserCommandHelper(args)); });
CommandHandler.RegisterCommand("unban", args => { return BanLists.HandleUnbanCommand(args); });

CommandHandler.RegisterCommand("send", args => {
    const string optionUsage = "Usage: send <stage> <id> <scenario[-1..127]> <player/*>";
    if (args.Length < 4) {
        return optionUsage;
    }

    string? stage = Stages.Input2Stage(args[0]);
    if (stage == null) {
        return "Invalid Stage Name! ```" + Stages.KingdomAliasMapping() + "```";
    }

    string id = args[1];

    if (!sbyte.TryParse(args[2], out sbyte scenario) || scenario < -1)
        return $"Invalid scenario number {args[2]} (range: [-1 to 127])";
    Client[] players = args[3] == "*"
        ? server.Clients.Where(c => c.Connected).ToArray()
        : server.Clients.Where(c =>
                c.Connected
                && args[3..].Any(x => c.Name.StartsWith(x) || (Guid.TryParse(x, out Guid result) && result == c.Id)))
            .ToArray();
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
    if (args.Length < 1) {
        return optionUsage;
    }

    string? stage = Stages.Input2Stage(args[0]);
    if (stage == null) {
        return "Invalid Stage Name! ```" + Stages.KingdomAliasMapping() + "```";
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
            return $"Scenario merging is {Settings.Instance.Scenario.MergeEnabled}";
        }
        default:
            return optionUsage;
    }
});

CommandHandler.RegisterCommand("tag", args => {
    const string optionUsage =
        "Valid options:\n\ttime <user/*> <minutes[0-65535]> <seconds[0-59]>\n\tseeking <user/*> <true/false>\n\tstart <time> <seekers>";
    if (args.Length < 3)
        return optionUsage;
    switch (args[0]) {
        case "time" when args.Length == 4: {
            if (args[1] != "*" && server.Clients.All(x => x.Name != args[1])) return $"Cannot find user {args[1]}";
            Client? client = server.Clients.FirstOrDefault(x => x.Name == args[1]);
            if (!ushort.TryParse(args[2], out ushort minutes))
                return $"Invalid time for minutes {args[2]} (range: 0-65535)";
            if (!byte.TryParse(args[3], out byte seconds) || seconds >= 60)
                return $"Invalid time for seconds {args[3]} (range: 0-59)";
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
                return
                    $"Couldn't find seeker{(seekerNames.Length > 1 ? "s" : "")}: {string.Join(", ", seekerNames.Where(name => server.Clients.All(c => c.Name != name)))}";
            Task.Run(async () => {
                int realTime = 1000 * time;
                await Task.Delay(realTime);
                await Task.WhenAll(
                    Parallel.ForEachAsync(seekers, async (seeker, _) =>
                        await server.Broadcast(new TagPacket {
                            UpdateType = TagPacket.TagUpdate.State,
                            IsIt = true
                        }, seeker)),
                    Parallel.ForEachAsync(server.Clients.Except(seekers), async (hider, _) =>
                        await server.Broadcast(new TagPacket {
                            UpdateType = TagPacket.TagUpdate.State,
                            IsIt = false
                        }, hider)
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

CommandHandler.RegisterCommand("maxplayers", args => {
    const string optionUsage = "Valid usage: maxplayers <playercount>";
    if (args.Length != 1) return optionUsage;
    if (!ushort.TryParse(args[0], out ushort maxPlayers)) return optionUsage;
    Settings.Instance.Server.MaxPlayers = maxPlayers;
    Settings.SaveSettings();
    foreach (Client client in server.Clients)
        client.Dispose(); // reconnect all players
    return $"Saved and set max players to {maxPlayers}";
});

CommandHandler.RegisterCommand("list",
    _ => $"List: {string.Join("\n\t", server.Clients.Where(x => x.Connected).Select(x => $"{x.Name} ({x.Id})"))}");

CommandHandler.RegisterCommand("flip", args => {
    const string optionUsage =
        "Valid options: \n\tlist\n\tadd <user id>\n\tremove <user id>\n\tset <true/false>\n\tpov <both/self/others>";
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
                string output = Settings.Instance.Flip.Players.Remove(result)
                    ? $"Removed {result} to flipped players"
                    : $"User {result} wasn't in the flipped players list";
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
    const string optionUsage = "Valid options: list, clear, sync, send, set, include, exclude";
    if (args.Length < 1)
        return optionUsage;
    switch (args[0]) {
        case "list" when args.Length == 1:
            return $"Shines: {string.Join(", ", shineBag)}" + (
                Settings.Instance.Shines.Excluded.Count() > 0
                ? "\nExcluded Shines: " + string.Join(", ", Settings.Instance.Shines.Excluded)
                : ""
            );
        case "clear" when args.Length == 1:
            shineBag.Clear();
            Task.Run(PersistShines);

            foreach (ConcurrentBag<int> playerBag in server.Clients.Select(serverClient =>
                (ConcurrentBag<int>)serverClient.Metadata["shineSync"]!)) playerBag?.Clear();

            return "Cleared shine bags";
        case "sync" when args.Length == 1:
            SyncShineBag();
            return "Synced shine bag automatically";
        case "send" when args.Length >= 3:
            if (int.TryParse(args[1], out int id)) {
                Client[] players = args[2] == "*"
                    ? server.Clients.Where(c => c.Connected).ToArray()
                    : server.Clients.Where(c => c.Connected && args[3..].Contains(c.Name)).ToArray();
                Parallel.ForEachAsync(players, async (c, _) => {
                    await c.Send(new ShinePacket {
                        ShineId = id
                    });
                }).Wait();
                return $"Sent Shine Num {id}";
            }

            return optionUsage;
        case "set" when args.Length == 2: {
            if (bool.TryParse(args[1], out bool result)) {
                Settings.Instance.Shines.Enabled = result;
                Settings.SaveSettings();
                return result ? "Enabled shine sync" : "Disabled shine sync";
            }

            return optionUsage;
        }
        case "exclude" when args.Length == 2:
        case "include" when args.Length == 2: {
            if (int.TryParse(args[1], out int sid)) {
                if (args[0] == "exclude") {
                    Settings.Instance.Shines.Excluded.Add(sid);
                    Settings.SaveSettings();
                    return $"Exclude shine {sid} from syncing.";
                } else {
                    Settings.Instance.Shines.Excluded.Remove(sid);
                    Settings.SaveSettings();
                    return $"No longer exclude shine {sid} from syncing.";
                }
            }
            return optionUsage;
        }
        default:
            return optionUsage;
    }
});

CommandHandler.RegisterCommand("loadsettings", _ => {
    Settings.LoadSettings();
    return "Loaded settings.json";
});

CommandHandler.RegisterCommand("restartserver", args =>
{
    if (args.Length != 0)
    {
        return "Usage: restartserver (no arguments)";
    }
    else
    {
        consoleLogger.Info("Received restartserver command");
        restartRequested = true;
        cts.Cancel();
        return "Restarting...";
    }
});

Console.CancelKeyPress += (_, e) => {
    e.Cancel = true;
    consoleLogger.Info("Received Ctrl+C");
    cts.Cancel();
};

CommandHandler.RegisterCommandAliases(_ => {
    cts.Cancel();
    return "Shutting down";
}, "exit", "quit", "q");

#pragma warning disable CS4014
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
}).ContinueWith(logError);
#pragma warning restore CS4014

await server.Listen(cts.Token);

if (restartRequested) //need to do this here because this needs to happen after the listener closes, and there isn't an
                      //easy way to sync in the restartserver command without it exiting Main()
{
    string? path = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name;
    const string unableToStartMsg = "Unable to ascertain the executable location, you'll need to re-run the server manually.";
    if (path != null) //path is probably just "Server", but in the context of the assembly, that's all you need to restart it.
    {
        Console.WriteLine($"Server Running on (pid): {System.Diagnostics.Process.Start(path)?.Id.ToString() ?? unableToStartMsg}");
    }
    else
        consoleLogger.Info(unableToStartMsg);
}
