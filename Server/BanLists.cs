using System.Net;
using System.Net.Sockets;
using System.Text;

using Shared;
using Shared.Packet.Packets;

namespace Server;

using MUCH = Func<string[], (HashSet<string> failToFind, HashSet<Client> toActUpon, List<(string arg, IEnumerable<string> amb)> ambig)>;

public static class BanLists {
    public static bool Enabled {
        get {
            return Settings.Instance.BanList.Enabled;
        }
        private set {
            Settings.Instance.BanList.Enabled = value;
        }
    }

    private static ISet<string> IPs {
        get {
            return Settings.Instance.BanList.IpAddresses;
        }
    }

    private static ISet<Guid> Profiles {
        get {
            return Settings.Instance.BanList.Players;
        }
    }

    private static ISet<string> Stages {
        get {
            return Settings.Instance.BanList.Stages;
        }
    }


    private static bool IsIPv4(string str) {
        return IPAddress.TryParse(str, out IPAddress? ip)
            && ip != null
            && ip.AddressFamily == AddressFamily.InterNetwork;
        ;
    }


    public static bool IsIPv4Banned(Client user) {
        IPEndPoint? ipv4 = (IPEndPoint?) user.Socket?.RemoteEndPoint;
        if (ipv4 == null) { return false; }
        return IsIPv4Banned(ipv4.Address);
    }
    public static bool IsIPv4Banned(IPAddress ipv4) {
        return IsIPv4Banned(ipv4.ToString());
    }
    public static bool IsIPv4Banned(string ipv4) {
        return IPs.Contains(ipv4);
    }

    public static bool IsProfileBanned(Client user) {
        return IsProfileBanned(user.Id);
    }
    public static bool IsProfileBanned(string str) {
        if (!Guid.TryParse(str, out Guid id)) { return false; }
        return IsProfileBanned(id);
    }
    public static bool IsProfileBanned(Guid id) {
        return Profiles.Contains(id);
    }

    public static bool IsStageBanned(string stage) {
        return Stages.Contains(stage);
    }

    public static bool IsClientBanned(Client user) {
        return IsProfileBanned(user) || IsIPv4Banned(user);
    }


    private static void BanIPv4(Client user) {
        IPEndPoint? ipv4 = (IPEndPoint?) user.Socket?.RemoteEndPoint;
        if (ipv4 != null) {
            BanIPv4(ipv4.Address);
        }
    }
    private static void BanIPv4(IPAddress ipv4) {
        BanIPv4(ipv4.ToString());
    }
    private static void BanIPv4(string ipv4) {
        IPs.Add(ipv4);
    }

    private static void BanProfile(Client user) {
        BanProfile(user.Id);
    }
    private static void BanProfile(string str) {
        if (!Guid.TryParse(str, out Guid id)) { return; }
        BanProfile(id);
    }
    private static void BanProfile(Guid id) {
        Profiles.Add(id);
    }

    private static void BanStage(string stage) {
        Stages.Add(stage);
    }

    private static void BanClient(Client user) {
        BanProfile(user);
        BanIPv4(user);
    }


    private static void UnbanIPv4(Client user) {
        IPEndPoint? ipv4 = (IPEndPoint?) user.Socket?.RemoteEndPoint;
        if (ipv4 != null) {
            UnbanIPv4(ipv4.Address);
        }
    }
    private static void UnbanIPv4(IPAddress ipv4) {
        UnbanIPv4(ipv4.ToString());
    }
    private static void UnbanIPv4(string ipv4) {
        IPs.Remove(ipv4);
    }

    private static void UnbanProfile(Client user) {
        UnbanProfile(user.Id);
    }
    private static void UnbanProfile(string str) {
        if (!Guid.TryParse(str, out Guid id)) { return; }
        UnbanProfile(id);
    }
    private static void UnbanProfile(Guid id) {
        Profiles.Remove(id);
    }

    private static void UnbanStage(string stage) {
        Stages.Remove(stage);
    }


    private static void Save() {
        Settings.SaveSettings(true);
    }


    public static void Crash(
        Client user,
        int  delay_ms  = 0
    ) {
        user.Ignored = true;
        Task.Run(async () => {
            if (delay_ms > 0) {
                await Task.Delay(delay_ms);
            }
            bool permanent = user.Banned;
            await user.Send(new ChangeStagePacket {
                Id              = (permanent ? "$agogus/ban4lyfe" : "$among$us/cr4sh%"),
                Stage           = (permanent ? "$ejected"         : "$agogusStage"),
                Scenario        = (sbyte) (permanent ? 69 : 21),
                SubScenarioType = (byte)  (permanent ? 21 : 69),
            });
        });
    }

    private static void CrashMultiple(string[] args, MUCH much) {
        foreach (Client user in much(args).toActUpon) {
            user.Banned = true;
            Crash(user);
        }
    }


    public static string HandleBanCommand(string[] args, MUCH much) {
        if (args.Length == 0) {
            return "Usage: ban {list|enable|disable|player|profile|ip|stage} ...";
        }

        string cmd = args[0];
        args = args.Skip(1).ToArray();

        switch (cmd) {
            default:
                return "Usage: ban {list|enable|disable|player|profile|ip|stage} ...";

            case "list":
                if (args.Length != 0) {
                    return "Usage: ban list";
                }
                StringBuilder list = new StringBuilder();
                list.Append("BanList: " + (Enabled ? "enabled" : "disabled"));

                if (IPs.Count > 0) {
                    list.Append("\nBanned IPv4 addresses:\n- ");
                    list.Append(string.Join("\n- ", IPs));
                }

                if (Profiles.Count > 0) {
                    list.Append("\nBanned profile IDs:\n- ");
                    list.Append(string.Join("\n- ", Profiles));
                }

                if (Stages.Count > 0) {
                    list.Append("\nBanned stages:\n- ");
                    list.Append(string.Join("\n- ", Stages));
                }

                return list.ToString();

            case "enable":
                if (args.Length != 0) {
                    return "Usage: ban enable";
                }
                Enabled = true;
                Save();
                return "BanList enabled.";

            case "disable":
                if (args.Length != 0) {
                    return "Usage: ban disable";
                }
                Enabled = false;
                Save();
                return "BanList disabled.";

            case "player":
                if (args.Length == 0) {
                    return "Usage: ban player <* | !* (usernames to not ban...) | (usernames to ban...)>";
                }

                var res = much(args);

                StringBuilder sb = new StringBuilder();
                sb.Append(res.toActUpon.Count  > 0 ? "Banned players: " + string.Join(", ", res.toActUpon.Select(x => $"\"{x.Name}\"")) : "");
                sb.Append(res.failToFind.Count > 0 ? "\nFailed to find matches for: " + string.Join(", ", res.failToFind.Select(x => $"\"{x.ToLower()}\"")) : "");
                if (res.ambig.Count > 0) {
                    res.ambig.ForEach(x => {
                        sb.Append($"\nAmbiguous for \"{x.arg}\": {string.Join(", ", x.amb.Select(x => $"\"{x}\""))}");
                    });
                }

                foreach (Client user in res.toActUpon) {
                    user.Banned = true;
                    BanClient(user);
                    Crash(user);
                }

                Save();
                return sb.ToString();

            case "profile":
                if (args.Length != 1) {
                    return "Usage: ban profile <profile-id>";
                }
                if (!Guid.TryParse(args[0], out Guid id)) {
                    return "Invalid profile ID value!";
                }
                if (IsProfileBanned(id)) {
                    return "Profile " + id.ToString() + " is already banned.";
                }
                BanProfile(id);
                CrashMultiple(args, much);
                Save();
                return "Banned profile: " + id.ToString();

            case "ip":
                if (args.Length != 1) {
                    return "Usage: ban ip <ipv4-address>";
                }
                if (!IsIPv4(args[0])) {
                    return "Invalid IPv4 address!";
                }
                if (IsIPv4Banned(args[0])) {
                    return "IP " + args[0] + " is already banned.";
                }
                BanIPv4(args[0]);
                CrashMultiple(args, much);
                Save();
                return "Banned ip: " + args[0];

            case "stage":
                if (args.Length != 1) {
                    return "Usage: ban stage <stage-name>";
                }
                string? stage = Shared.Stages.Input2Stage(args[0]);
                if (stage == null) {
                    return "Invalid stage name!";
                }
                if (IsStageBanned(stage)) {
                    return "Stage " + stage + " is already banned.";
                }
                var stages = Shared.Stages
                    .StagesByInput(args[0])
                    .Where(s => !IsStageBanned(s))
                    .ToList()
                ;
                foreach (string s in stages) {
                    BanStage(s);
                }
                Save();
                return "Banned stage: " + string.Join(", ", stages);
        }
    }


    public static string HandleUnbanCommand(string[] args) {
        if (args.Length != 2) {
            return "Usage: unban {profile|ip|stage} <value>";
        }

        string cmd = args[0];
        string val = args[1];

        switch (cmd) {
            default:
                return "Usage: unban {profile|ip|stage} <value>";

            case "profile":
                if (!Guid.TryParse(val, out Guid id)) {
                    return "Invalid profile ID value!";
                }
                if (!IsProfileBanned(id)) {
                    return "Profile " + id.ToString() + " is not banned.";
                }
                UnbanProfile(id);
                Save();
                return "Unbanned profile: " + id.ToString();

            case "ip":
                if (!IsIPv4(val)) {
                    return "Invalid IPv4 address!";
                }
                if (!IsIPv4Banned(val)) {
                    return "IP " + val + " is not banned.";
                }
                UnbanIPv4(val);
                Save();
                return "Unbanned ip: " + val;

            case "stage":
                string stage = Shared.Stages.Input2Stage(val) ?? val;
                if (!IsStageBanned(stage)) {
                    return "Stage " + stage + " is not banned.";
                }
                var stages = Shared.Stages
                    .StagesByInput(val)
                    .Where(IsStageBanned)
                    .ToList()
                ;
                foreach (string s in stages) {
                    UnbanStage(s);
                }
                Save();
                return "Unbanned stage: " + string.Join(", ", stages);
        }
    }
}
