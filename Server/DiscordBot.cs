using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;
using Shared;

namespace Server;

public class DiscordBot {
    private DiscordClient? DiscordClient;
    private string? Token;
    private Settings.DiscordTable Config => Settings.Instance.Discord;
    private string Prefix => Config.Prefix;
    private readonly Logger Logger = new Logger("Discord");
    private DiscordChannel? LogChannel;

    public DiscordBot() {
        Token = Config.Token;
        Logger.AddLogHandler(Log);
        if (Config.Token == null) return;
        Settings.LoadHandler += SettingsLoadHandler;
    }

    private async void SettingsLoadHandler() {
        try {
            if (DiscordClient == null || Token != Config.Token)
                Run();
            if (Config.LogChannel != null)
                LogChannel = await (DiscordClient?.GetChannelAsync(ulong.Parse(Config.LogChannel)) ??
                                    throw new NullReferenceException("Discord client not setup yet!"));
        } catch (Exception e) {
            Logger.Error($"Failed to get log channel \"{Config.LogChannel}\"");
            Logger.Error(e);
        }
    }

    private async void Log(string source, string level, string text, ConsoleColor _) {
        try {
            if (DiscordClient != null && LogChannel != null) {
                await DiscordClient.SendMessageAsync(LogChannel,
                    $"Console log:```{Logger.PrefixNewLines(text, $"{level} [{source}]")}```");
            }
        } catch (Exception e) {
            await Console.Error.WriteLineAsync("Exception in discord logger");
            await Console.Error.WriteLineAsync(e.ToString());
        }
    }

    public async void Run() {
        Token = Config.Token;
        DiscordClient?.Dispose();
        if (Config.Token == null) {
            DiscordClient = null;
            return;
        }

        try {
            DiscordClient = new DiscordClient(new DiscordConfiguration {
                Token = Config.Token,
                MinimumLogLevel = LogLevel.None
            });
            await DiscordClient.ConnectAsync(new DiscordActivity("Hide and Seek", ActivityType.Competing));
            SettingsLoadHandler();
            string mentionPrefix = $"{DiscordClient.CurrentUser.Mention} ";
            DiscordClient.MessageCreated += async (_, args) => {
                DiscordMessage msg = args.Message;
                Console.WriteLine(
                    $"{msg.Content} {DiscordClient.CurrentUser.Mention} {msg.Content.StartsWith(mentionPrefix)}");
                if (msg.Content.StartsWith(Prefix)) {
                    await msg.Channel.TriggerTypingAsync();
                    await msg.RespondAsync(string.Join('\n',
                        CommandHandler.GetResult(msg.Content[Prefix.Length..]).ReturnStrings));
                } else if (msg.Content.StartsWith(mentionPrefix)) {
                    await msg.Channel.TriggerTypingAsync();
                    await msg.RespondAsync(string.Join('\n',
                        CommandHandler.GetResult(msg.Content[mentionPrefix.Length..]).ReturnStrings));
                }
            };
        } catch (Exception e) {
            Logger.Error("Exception occurred in discord runner!");
            Logger.Error(e);
        }
    }
}