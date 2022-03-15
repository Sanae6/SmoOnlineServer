using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Shared;

namespace Server;

public class Settings {
    public static Settings Instance = new Settings();
    private static readonly Logger Logger = new Logger("Settings");

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
        } else {
            SaveSettings();
        }
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

    public class ServerTable {
        public string Address { get; set; } = IPAddress.Any.ToString();
        public ushort Port { get; set; } = 1027;
        public byte MaxPlayers { get; set; } = 8;
    }

    public class ScenarioTable {
        public bool MergeEnabled { get; set; } = false;
    }

    public class FlipTable {
        public List<Guid> Players { get; set; } = new List<Guid>();
        public bool Enabled { get; set; } = true;
        public FlipOptions Pov { get; set; } = FlipOptions.Both;
    }
}