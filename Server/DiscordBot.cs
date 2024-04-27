using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Shared;

namespace Server;

public class DiscordBot
{
    private readonly Logger logger = new Logger("Discord");
    private Settings.DiscordTable localSettings = Settings.Instance.Discord;
    private DiscordSocketClient? client = null;
    private SocketTextChannel? logChannel = null;
    private bool firstInitTriggered = false;

    //check how this works with neither, one or the other, or both channels set.

    public DiscordBot()
    {
        CommandHandler.RegisterCommand("dscrestart", _ =>
        {
            //Task.Run is to fix deadlock (dispose can only be finalized if all discord callbacks are returned,
            //and since this delegate is called directly from a callback, it would cause a deadlock).
            Task.Run(() =>
            {
                Stop();
#pragma warning disable CS4014
                Init();
#pragma warning restore CS4014
            });
            return localSettings.Enabled ? "Restarting Discord bot..." : "The discord bot is disabled in settings.json (no action was taken).";
        });
        if (localSettings.Enabled)
            logger.Info("Starting discord bot (ctor)");
        Settings.LoadHandler += OnLoadSettings;
        Logger.AddLogHandler(LogToDiscordLogChannel);
    }

    //this nonsense is to prevent race conditions from starting multiple bots.
    //this would be a great thing to instead simply have an "await Init()" put
    //in the ctor (but awaits can't be there), and Task.Wait shouldn't be used that way.
    private object firstInitLockObj = new object();
    public async Task FirstInit()
    {
        lock (firstInitLockObj)
        {
            if (firstInitTriggered)
                return;
            firstInitTriggered = true;
        }
        await Init();
    }

    private async Task Init()
    {
        if (client != null || !localSettings.Enabled)
        {
            return; //Either: the discord bot is disabled, or: this is bad if the client ever crashes and
                    //isn't reassigned to null, but we don't want multiple instances of the bot running at the same time.
        }
        if (localSettings.Token == null || (localSettings.AdminChannel == null && localSettings.CommandChannel == null))
        {
            //no point trying to run anything if there's no discord token and/or no channel for a user to interact with the bot through.
            logger.Error("Tried to run the discord bot, but the Token and/or communication channels are not specified in the settings.");
            return;
        }
        client = new DiscordSocketClient(
            new DiscordSocketConfig()
            {
                LogLevel = LogSeverity.Warning
            });

        client.Log += async (a) => await Task.Run(() => 
        {
            //as time goes on, we may encounter logged info that we literally don't care about. Fill out an if statement to properly
            //filter it out to avoid logging it to discord.
            if (localSettings.FilterOutNonIssueWarnings)
            {
                string[] disinterestedMessages =
                { //these messages happen sometimes, and are of no concern.
                    "Server requested a reconnect",
                    "The remote party closed the WebSocket connection without completing the close handshake",
                    "without listening to any events related to that intent, consider removing the intent from"
                };
                foreach (string dis in disinterestedMessages)
                {
                    if ((a.Exception?.ToString().Contains(dis) ?? false) ||
                         (a.Message?.Contains(dis) ?? false))
                    {
                        return;
                    }
                }
            }
            string message = a.Message ?? string.Empty + (a.Exception != null ? "Exception: " + a.Exception.ToString() : ""); //TODO: this crashes
            ConsoleColor col;
            switch (a.Severity)
            {
                default:
                case LogSeverity.Info:
                case LogSeverity.Debug:
                    col = ConsoleColor.White;
                    break;
                case LogSeverity.Critical:
                case LogSeverity.Error:
                    col = ConsoleColor.Red;
                    break;
                case LogSeverity.Warning:
                    col = ConsoleColor.Yellow;
                    break;
            }

            LogToDiscordLogChannel($"Discord: {a.Source}", a.Severity.ToString(), message, col);
        });
        try
        {
            SemaphoreSlim wait = new SemaphoreSlim(0);
#pragma warning disable CS1998
            client.Ready += async () =>
#pragma warning restore CS1998
            {
                wait.Release();
            };
            await client.LoginAsync(Discord.TokenType.Bot, localSettings.Token);
            await client.StartAsync();
            await wait.WaitAsync();
            //we need to wait for the ready event before we can do any of this nonsense.
            logChannel = (ulong.TryParse(localSettings.AdminChannel, out ulong lcid) ? (client != null ? await client.GetChannelAsync(lcid) : null) : null) as SocketTextChannel;
            client!.MessageReceived += (m) => HandleCommandAsync(m);
            logger.Info("Discord bot has been initialized.");
        }
        catch (Exception e)
        {
            logger.Error(e);
        }
    }

    private void Stop()
    {
        try
        {
            if (client != null)
            {
                if (!client.StopAsync().Wait(60000))
                    logger.Warn("Tried to stop the discord bot, but attempt took >60 seconds, so it failed!");
            }
            client?.Dispose();
        }
        catch { /*lol (lmao)*/ }
        client = null;
        logChannel = null;
        localSettings = Settings.Instance.Discord;
    }

    private async void LogToDiscordLogChannel(string source, string level, string text, ConsoleColor color)
    {
        logChannel = (ulong.TryParse(localSettings.AdminChannel, out ulong lcid) ? (client != null ? await client.GetChannelAsync(lcid) : null) : null) as SocketTextChannel;
        if (logChannel != null)
        {
            try
            {
                switch (color)
                {
                    //I looked into other hacky methods of doing more colors, the rest seemed unreliable.
                    default:
                        foreach (string mesg in SplitMessage(Logger.PrefixNewLines(text, $"{level} [{source}]"), 1994)) //room for 6 '`'
                            await logChannel.SendMessageAsync($"```{mesg}```");
                        break;
                    case ConsoleColor.Yellow: //this is actually light blue now (discord changed it awhile ago).
                        foreach (string mesg in SplitMessage(Logger.PrefixNewLines(text, $"{level} [{source}]"), 1990)) //room for 6 '`', "fix" and "\n"
                            await logChannel.SendMessageAsync($"```fix\n{mesg}```");
                        break;
                    case ConsoleColor.Red:
                        foreach (string mesg in SplitMessage(Logger.PrefixNewLines(text, $"-{level} [{source}]"), 1989)) //room for 6 '`', "diff" and "\n"
                            await logChannel.SendMessageAsync($"```diff\n{mesg}```");
                        break;
                    case ConsoleColor.Green:
                        foreach (string mesg in SplitMessage(Logger.PrefixNewLines(text, $"+{level} [{source}]"), 1989)) //room for 6 '`', "diff" and "\n"
                            await logChannel.SendMessageAsync($"```diff\n{mesg}```");
                        break;
                }
            }
            catch (Exception e)
            {
                // don't log again, it'll just stack overflow the server!
                await Console.Error.WriteLineAsync("Exception in discord logger");
                await Console.Error.WriteLineAsync(e.ToString());
            }
        }
    }

    private async Task HandleCommandAsync(SocketMessage arg)
    {
        if (arg is not SocketUserMessage)
            return; //idk what to do in this circumstance.
        if ((arg.Channel.Id.ToString() == localSettings.CommandChannel || arg.Channel.Id.ToString() == localSettings.AdminChannel) && !arg.Author.IsBot)
        {
            string message = (await arg.Channel.GetMessageAsync(arg.Id)).Content;
            //run command
            try
            {
                string? args = null;
                if (string.IsNullOrEmpty(localSettings.Prefix))
                {
                    args = message;
                }
                else if (message.StartsWith(localSettings.Prefix))
                {
                    args = message[localSettings.Prefix.Length..];
                }
                else if (message.StartsWith($"<@{client!.CurrentUser.Id}>"))
                {
                    args = message[client!.CurrentUser.Mention.Length..].TrimStart();
                }
                if (args != null)
                {
                    await arg.Channel.TriggerTypingAsync();
                    string resp = string.Join('\n', CommandHandler.GetResult(args).ReturnStrings);
                    if (localSettings.LogCommands)
                    {
                        logger.Info($"\"{arg.Author.Username}\" ran the command: \"{message}\" via discord");
                    }
                    foreach (string mesg in SplitMessage(resp))
                        await (arg as SocketUserMessage).ReplyAsync(mesg);
                }
            }
            catch (Exception e)
            {
                logger.Error(e);
            }
        }
        else
        {
            //don't respond to commands not in these channels, and no bots
            //probably don't print out error message because DDOS
        }
    }

    private void OnLoadSettings()
    {
        Settings.DiscordTable oldSettings = localSettings;
        localSettings = Settings.Instance.Discord;

        if (localSettings.CommandChannel == null)
            logger.Warn("You probably should set your CommandChannel in settings.json");
        if (localSettings.AdminChannel == null)
            logger.Warn("You probably should set your AdminChannel in settings.json");

        if (oldSettings.Token != localSettings.Token || oldSettings.AdminChannel != localSettings.AdminChannel || oldSettings.CommandChannel != localSettings.CommandChannel)
        {
            //start over fresh (there might be a more intelligent way to do this without restarting the bot if only the log/command channel changed, but I'm lazy.
            Stop();
#pragma warning disable CS4014
            Init();
#pragma warning restore CS4014
        }
    }

    private static List<string> SplitMessage(string message, int maxSizePerElem = 2000)
    {
        List<string> result = new List<string>();
        for (int i = 0; i < message.Length; i += maxSizePerElem)
        {
            result.Add(message.Substring(i, message.Length - i < maxSizePerElem ? message.Length - i : maxSizePerElem));
        }
        return result;
    }

    ~DiscordBot()
    {
        Stop();
    }
}
