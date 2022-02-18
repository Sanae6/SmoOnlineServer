using System.Net;
using Shared;
using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Syntax;

namespace Server; 

public class Settings {
    public static Settings Instance = new Settings();
    private static readonly Logger Logger = new Logger("Settings");
    static Settings() {
        LoadSettings();
    }

    public static void LoadSettings() {
        if (File.Exists("settings.toml")) {
            string text = File.ReadAllText("settings.toml");
            if (Toml.TryToModel(text, out Settings? settings, out DiagnosticsBag? bag, options: new TomlModelOptions() {
                    ConvertTo = (value, _) => {
                        if (value is string str && Guid.TryParse(str, out Guid result))
                            return result;

                        return null;
                    }
                }))
                Logger.Info("Loaded settings from settings.toml");
            else
                Logger.Warn($"Failed to load settings.toml: {bag}");
            if (settings != null) Instance = settings;
        } else {
            SaveSettings();
        }
    }

    public static void SaveSettings(Settings? settings = null) {
        try {
            File.WriteAllText("settings.toml", Toml.FromModel(settings ?? Instance!, new TomlModelOptions {
                ConvertTo = (x, _) => {
                    if (x is Guid guid)
                        return guid.ToString();

                    return null!;
                }
            }));
            Logger.Info("Saved settings to settings.toml");
        } catch (Exception e) {
            Logger.Error($"Failed to save settings.toml {e}");
        }
    }

    public ServerTable Server { get; set; } = new ServerTable();
    public FlipTable Flip { get; set; } = new FlipTable();

    public class ServerTable {
        public string Address { get; set; } = IPAddress.Any.ToString();
        public ushort Port { get; set; } = 1027;
    }

    public class FlipTable {
        public List<Guid> Players { get; set; } = new List<Guid>();
        public bool EnabledOnStart { get; set; } = true;
        public FlipOptions Pov { get; set; } = FlipOptions.Both;
    }
}