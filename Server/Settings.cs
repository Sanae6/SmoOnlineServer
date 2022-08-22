using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Shared;

namespace Server;

public class Settings {
    public static Settings Instance = new Settings();
    private static readonly Logger Logger = new Logger("Settings");
    public static Action? LoadHandler;

    static Settings() {
        LoadSettings();
    }

    public static void LoadSettings() {
        if (File.Exists("settings.json")) {
            string text = File.ReadAllText("settings.json");
            try {
                Instance = JsonConvert.DeserializeObject<Settings>(text, new StringEnumConverter(new CamelCaseNamingStrategy())) ?? Instance;
                Logger.Info("Loaded settings from settings.json");
            }
            catch (Exception e) {
                Logger.Warn($"Failed to load settings.json: {e}");
            }
        }
        SaveSettings();
        LoadHandler?.Invoke();
    }

    public static void SaveSettings() {
        try {
            File.WriteAllText("settings.json", JsonConvert.SerializeObject(Instance, Formatting.Indented, new StringEnumConverter(new CamelCaseNamingStrategy())));
            Logger.Info("Saved settings to settings.json");
        }
        catch (Exception e) {
            Logger.Error($"Failed to save settings.json {e}");
        }
    }

    public ServerTable Server { get; set; } = new ServerTable();
    public FlipTable Flip { get; set; } = new FlipTable();
    public ScenarioTable Scenario { get; set; } = new ScenarioTable();
    public BannedPlayers BanList { get; set; } = new BannedPlayers();
    public DiscordTable Discord { get; set; } = new DiscordTable();
    public ShineTable Shines { get; set; } = new ShineTable();
    public PersistShinesTable PersistShines { get; set; } = new PersistShinesTable();

    public class ServerTable {
        public string Address { get; set; } = IPAddress.Any.ToString();
        public ushort Port { get; set; } = 1027;
        public ushort MaxPlayers { get; set; } = 8;
    }

    public class ScenarioTable {
        public bool MergeEnabled { get; set; } = false;
    }

    public class BannedPlayers {
        public bool Enabled { get; set; } = false;
        public List<Guid> Players { get; set; } = new List<Guid>();
        public List<string> IpAddresses { get; set; } = new List<string>();
    }

    public class FlipTable {
        public bool Enabled { get; set; } = true;
        public List<Guid> Players { get; set; } = new List<Guid>();
        public FlipOptions Pov { get; set; } = FlipOptions.Both;
    }

    public class DiscordTable {
        public string? Token { get; set; }
        public string Prefix { get; set; } = "$";
        public string? CommandChannel { get; set; }
        public string? LogChannel { get; set; }
    }

    public class ShineTable {
        public bool Enabled { get; set; } = true;
    }

    public class PersistShinesTable
    {
        public bool Enabled { get; set; } = false;
        public string Filename { get; set; } = "./moons.json";
    }
}