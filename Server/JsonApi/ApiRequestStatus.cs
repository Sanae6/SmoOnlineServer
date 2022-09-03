using Shared;
using Shared.Packet.Packets;
using System.Net;
using System.Numerics;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Server.JsonApi;

using Mutators = Dictionary<string, Action<ApiRequestStatus.Player, Client>>;

public static class ApiRequestStatus {
    public static async Task<bool> Send(Context ctx) {
        StatusResponse resp = new StatusResponse {
            Settings = ApiRequestStatus.GetSettings(ctx),
            Players  = ApiRequestStatus.Player.GetPlayers(ctx),
        };
        await ctx.Send(resp);
        return true;
    }


    private static JsonNode? GetSettings(Context ctx)
    {
        // output object
        JsonObject settings = new JsonObject();

        // all permissions for Settings
        var allowedSettings = ctx.Permissions
            .Where(str => str.StartsWith("Status/Settings/"))
            .Select(str => str.Substring(16))
        ;

        bool has_results = false;

        // copy all allowed Settings
        foreach (string allowedSetting in allowedSettings) {
            string lastKey = "";
            JsonNode? next  = settings;
            object input = Settings.Instance;
            JsonObject output = settings!;

            // recursively go down the path
            foreach (string key in allowedSetting.Split("/")) {
                lastKey = key;

                if (next == null) { break; }
                output = (JsonObject) next!;

                // create the sublayer
                if (!output.ContainsKey(key)) { output.Add(key, new JsonObject()); }

                // traverse down the output object
                output.TryGetPropertyValue(key, out next);

                // traverse down the Settings object
                var prop = input.GetType().GetProperty(key);
                if (prop == null) {
                    JsonApi.Logger.Warn($"Property \"{allowedSetting}\" doesn't exist on the Settings object. This is probably a misconfiguration in the settings.json");
                    goto continue2;
                } else {
                    input = prop.GetValue(input, null)!;
                }
            }

            if (lastKey != "") {
                // copy key with the actual value
                output.Remove(lastKey);
                output.Add(lastKey, JsonValue.Create(input));
                has_results = true;
            }

            continue2:;
        }

        if (!has_results) { return null; }
        return settings;
    }


    private class StatusResponse {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonNode? Settings { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Player[]? Players  { get; set; }
    }


    public class Player {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Guid? ID { get; private set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Name { get; private set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GameMode? GameMode { get; private set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Kingdom { get; private set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Stage { get; private set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Scenario { get; private set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public PlayerPosition? Position { get; private set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public PlayerRotation? Rotation { get; private set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Tagged { get; private set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public PlayerCostume? Costume { get; private set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Capture { get; private set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Is2D { get; private set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? IPv4 { get; private set; }


        private static Mutators Mutators = new Mutators {
            ["Status/Players/ID"]       = (Player p, Client c) => p.ID       = c.Id,
            ["Status/Players/Name"]     = (Player p, Client c) => p.Name     = c.Name,
            ["Status/Players/GameMode"] = (Player p, Client c) => p.GameMode = Player.GetGameMode(c),
            ["Status/Players/Kingdom"]  = (Player p, Client c) => p.Kingdom  = Player.GetKingdom(c),
            ["Status/Players/Stage"]    = (Player p, Client c) => p.Stage    = Player.GetGamePacket(c)?.Stage ?? null,
            ["Status/Players/Scenario"] = (Player p, Client c) => p.Scenario = Player.GetGamePacket(c)?.ScenarioNum ?? null,
            ["Status/Players/Position"] = (Player p, Client c) => p.Position = PlayerPosition.FromVector3(Player.GetPlayerPacket(c)?.Position ?? null),
            ["Status/Players/Rotation"] = (Player p, Client c) => p.Rotation = PlayerRotation.FromQuaternion(Player.GetPlayerPacket(c)?.Rotation ?? null),
            ["Status/Players/Tagged"]   = (Player p, Client c) => p.Tagged   = Player.GetTagged(c),
            ["Status/Players/Costume"]  = (Player p, Client c) => p.Costume  = PlayerCostume.FromClient(c),
            ["Status/Players/Capture"]  = (Player p, Client c) => p.Capture  = Player.GetCapture(c),
            ["Status/Players/Is2D"]     = (Player p, Client c) => p.Is2D     = Player.GetGamePacket(c)?.Is2d ?? null,
            ["Status/Players/IPv4"]     = (Player p, Client c) => p.IPv4     = (c.Socket?.RemoteEndPoint as IPEndPoint)?.Address.ToString(),
        };


        public static Player[]? GetPlayers(Context ctx) {
            if (!ctx.HasPermission("Status/Players"))  { return null; }
            return ctx.server.ClientsConnected.Select((Client c) => Player.FromClient(ctx, c)).ToArray();
        }


        private static Player FromClient(Context ctx, Client c) {
            Player player = new Player();
            foreach (var (perm, mutate) in Mutators) {
                if (ctx.HasPermission(perm))  {
                    mutate(player, c);
                }
            }
            return player;
        }


        private static GamePacket? GetGamePacket(Client c) {
            object? lastGamePacket = null;
            c.Metadata.TryGetValue("lastGamePacket", out lastGamePacket);
            if (lastGamePacket == null) { return null; }
            return (GamePacket) lastGamePacket;
        }


        private static PlayerPacket? GetPlayerPacket(Client c) {
            object? lastPlayerPacket = null;
            c.Metadata.TryGetValue("lastPlayerPacket", out lastPlayerPacket);
            if (lastPlayerPacket == null) { return null; }
            return (PlayerPacket) lastPlayerPacket;
        }


        private static GameMode? GetGameMode(Client c) {
            object? gamemode = null;
            c.Metadata.TryGetValue("gameMode", out gamemode);
            return (GameMode?) gamemode;
        }


        private static bool? GetTagged(Client c) {
            object? seeking = null;
            c.Metadata.TryGetValue("seeking", out seeking);
            return (bool?) seeking;
        }


        private static string? GetCapture(Client c) {
            object? lastCapturePacket = null;
            c.Metadata.TryGetValue("lastCapturePacket", out lastCapturePacket);
            if (lastCapturePacket == null) { return null; }
            CapturePacket p = (CapturePacket) lastCapturePacket;
            if (p.ModelName == "") { return null; }
            return p.ModelName;
        }


        private static string? GetKingdom(Client c) {
            string? stage = Player.GetGamePacket(c)?.Stage ?? null;
            if (stage == null) { return null; }

            Stages.Stage2Alias.TryGetValue(stage, out string? alias);
            if (alias == null) { return null; }

            if (Stages.Alias2Kingdom.Contains(alias)) {
                return (string?) Stages.Alias2Kingdom[alias];
            }

            return null;
        }
    }


    public class PlayerCostume {
        public string Cap  { get; private set; }
        public string Body { get; private set; }


        private PlayerCostume(CostumePacket p) {
            this.Cap  = p.CapName;
            this.Body = p.BodyName;
        }


        public static PlayerCostume? FromClient(Client c) {
            if (c.CurrentCostume == null) { return null; }
            CostumePacket p = (CostumePacket) c.CurrentCostume!;
            return new PlayerCostume(p);
        }
    }


    public class PlayerPosition {
        public float X { get; private set; }
        public float Y { get; private set; }
        public float Z { get; private set; }


        private PlayerPosition(float X, float Y, float Z) {
            this.X = X;
            this.Y = Y;
            this.Z = Z;
        }


        public static PlayerPosition? FromVector3(Vector3? pos) {
            if (pos == null) { return null; }
            Vector3 p = (Vector3) pos;
            return new PlayerPosition(p.X, p.Y, p.Z);
        }
    }


    public class PlayerRotation {
        public float W { get; private set; }
        public float X { get; private set; }
        public float Y { get; private set; }
        public float Z { get; private set; }


        private PlayerRotation(float W, float X, float Y, float Z) {
            this.W = W;
            this.X = X;
            this.Y = Y;
            this.Z = Z;
        }


        public static PlayerRotation? FromQuaternion(Quaternion? quat) {
            if (quat == null) { return null; }
            Quaternion q = (Quaternion) quat;
            return new PlayerRotation(q.W, q.X, q.Y, q.Z);
        }
    }
}
