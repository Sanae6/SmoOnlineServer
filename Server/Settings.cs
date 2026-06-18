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
				if (Instance.StageSelection == null || Instance.StageSelection.Count < 1)
					Instance.StageSelection = StageSelectionTable.Preset();
			}
            catch (Exception e) {
                Logger.Warn($"Failed to load settings.json: {e}");
            }
        }
        SaveSettings();
        LoadHandler?.Invoke();
    }

    public static void SaveSettings(bool silent = false) {
        try {
            File.WriteAllText("settings.json", JsonConvert.SerializeObject(Instance, Formatting.Indented, new StringEnumConverter(new CamelCaseNamingStrategy())));
            if (!silent) { Logger.Info("Saved settings to settings.json"); }
        }
        catch (Exception e) {
            Logger.Error($"Failed to save settings.json {e}");
        }
    }

    public ServerTable Server { get; set; } = new ServerTable();
    public FlipTable Flip { get; set; } = new FlipTable();
    public ScenarioTable Scenario { get; set; } = new ScenarioTable();
    public BanListTable BanList { get; set; } = new BanListTable();
    public List<StageSelectionTable> StageSelection { get; set; } = new List<StageSelectionTable>();
    public DiscordTable Discord { get; set; } = new DiscordTable();
    public ShineTable Shines { get; set; } = new ShineTable();
    public PersistShinesTable PersistShines { get; set; } = new PersistShinesTable();
    public JsonApiTable JsonApi { get; set; } = new JsonApiTable();

    public class ServerTable {
        public string Address { get; set; } = IPAddress.Any.ToString();
        public ushort Port { get; set; } = 1027;
        public ushort MaxPlayers { get; set; } = 8;
    }

    public class ScenarioTable {
        public bool MergeEnabled { get; set; } = false;
    }

    public class BanListTable {
        public bool Enabled { get; set; } = false;
        public ISet<Guid> Players { get; set; } = new SortedSet<Guid>();
        public ISet<string> IpAddresses { get; set; } = new SortedSet<string>();
        public ISet<string> Stages { get; set; } = new SortedSet<string>();
        public ISet<sbyte> GameModes { get; set; } = new SortedSet<sbyte>();
    }

	public class StageSelectionTable
	{
		public string stageName { get; set; }
		public bool enabled { get; set; } = true;
		public StageSelectionTable(string name)
		{
			stageName = name;
		}
		public static List<StageSelectionTable> Preset()
		{
			List<StageSelectionTable> list = new List<StageSelectionTable>
			{
				new StageSelectionTable("CapWorldHomeStage"),
				new StageSelectionTable("WaterfallWorldHomeStage"),
				new StageSelectionTable("SandWorldHomeStage"),
				new StageSelectionTable("LakeWorldHomeStage"),
				new StageSelectionTable("ForestWorldHomeStage"),
				new StageSelectionTable("CloudWorldHomeStage") { enabled = false },
				new StageSelectionTable("ClashWorldHomeStage"),
				new StageSelectionTable("CityWorldHomeStage"),
				new StageSelectionTable("SnowWorldHomeStage"),
				new StageSelectionTable("SeaWorldHomeStage"),
				new StageSelectionTable("LavaWorldHomeStage"),
				new StageSelectionTable("BossRaidWorldHomeStage") { enabled = false },
				new StageSelectionTable("SkyWorldHomeStage"),
				new StageSelectionTable("MoonWorldHomeStage"),
				new StageSelectionTable("PeachWorldHomeStage"),
				new StageSelectionTable("Special1WorldHomeStage") { enabled = false },
				new StageSelectionTable("Special2WorldHomeStage") { enabled = false },
			};
			return list;
		}
	}

	public class FlipTable {
        public bool Enabled { get; set; } = true;
        public ISet<Guid> Players { get; set; } = new SortedSet<Guid>();
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
        public ISet<int> Excluded { get; set; } = new SortedSet<int> { 496 };
        public bool ClearOnNewSaves { get; set; } = false;
    }

    public class PersistShinesTable
    {
        public bool Enabled { get; set; } = false;
        public string Filename { get; set; } = "./moons.json";
    }

    public class JsonApiTable
    {
        public bool Enabled { get; set; } = false;
        public Dictionary<string, SortedSet<string>> Tokens { get; set; } = new Dictionary<string, SortedSet<string>>();
    }
}
