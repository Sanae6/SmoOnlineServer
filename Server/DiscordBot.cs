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
    //private SocketTextChannel? commandChannel = null;
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
            return "Restarting Discord bot...";
        });
        logger.Info("Starting discord bot (ctor)");
        Settings.LoadHandler += OnLoadSettings;
        Logger.AddLogHandler(LogToDiscordLogChannel);
    }

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
        if (client != null)
        {
            return; //this is bad if the client ever crashes and isn't reassigned to null, but we don't want multiple instances of the bot running at the same time.
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
                if (a.Message.Contains("Server requested a reconnect"))
                {
                    //This is to filter out this message. This warning is for discord server load balancing and isn't a problem

                    //Warning[Discord: Gateway] Discord.WebSocket.GatewayReconnectException: Server requested a reconnect
                    //Warning[Discord: Gateway]    at Discord.ConnectionManager.<> c__DisplayClass29_0.<< StartAsync > b__0 > d.MoveNext()
                    return;
                }
            }
            string message = a.Message + (a.Exception != null ? "Exception: " + a.Exception.ToString() : "");
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
            await client.LoginAsync(Discord.TokenType.Bot, localSettings.Token);
            await client.StartAsync();
            SemaphoreSlim wait = new SemaphoreSlim(0);
#pragma warning disable CS1998
            client.Ready += async () =>
#pragma warning restore CS1998
            {
                wait.Release();
            };
            await wait.WaitAsync();
            //we need to wait for the ready event before we can do any of this nonsense.
            logChannel = (ulong.TryParse(localSettings.AdminChannel, out ulong lcid) ? (client != null ? await client.GetChannelAsync(lcid) : null) : null) as SocketTextChannel;
            //commandChannel = (ulong.TryParse(localSettings.CommandChannel, out ulong ccid) ? (client != null ? await client.GetChannelAsync(ccid) : null) : null) as SocketTextChannel;
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
                client.StopAsync().Wait();
            client?.Dispose();
        }
        catch { /*lol (lmao)*/ }
        client = null;
        logChannel = null;
        //commandChannel = null;
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
                    case ConsoleColor.Yellow:
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
            logger.Warn("You probably should set your LogChannel in settings.json");

        if (oldSettings.Token != localSettings.Token || oldSettings.AdminChannel != localSettings.AdminChannel || oldSettings.CommandChannel != localSettings.CommandChannel)
        {
            //start over fresh (there might be a more intelligent way to do this without restarting the bot if only the log/command channel changed, but I'm lazy.
            Stop();
#pragma warning disable CS4014
            Init();
#pragma warning restore CS4014
        }
    }

    private async Task WeGotRateLimitedLBozo(IRateLimitInfo info)
    {
        //this is spamming because apparently this is called for more than just rate limiting.
        //await Console.Error.WriteLineAsync("We got rate limited!");
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

    #region Old
    //private DiscordClient? DiscordClient;
    //private string? Token;
    //private Settings.DiscordTable Config => Settings.Instance.Discord;
    //private string Prefix => Config.Prefix;
    //private readonly Logger Logger = new Logger("Discord");
    //private DiscordChannel? CommandChannel;
    //private DiscordChannel? LogChannel;
    //private bool Reconnecting;

    //public DiscordBot() {
    //    Token = Config.Token;
    //    Logger.AddLogHandler(Log);
    //    CommandHandler.RegisterCommand("dscrestart", _ => {
    //        // this should be async'ed but i'm lazy
    //        Reconnecting = true;
    //        Task.Run(Reconnect);
    //        return "Restarting Discord bot";
    //    });
    //    if (Config.Token == null) return;
    //    if (Config.CommandChannel == null)
    //        Logger.Warn("You probably should set your CommandChannel in settings.json");
    //    if (Config.LogChannel == null)
    //        Logger.Warn("You probably should set your LogChannel in settings.json");
    //    Settings.LoadHandler += SettingsLoadHandler;
    //}

    //private async Task Reconnect() {
    //    if (DiscordClient != null) // usually null prop works, not here though...`
    //        await DiscordClient.DisconnectAsync();
    //    await Run();
    //}

    //private async void SettingsLoadHandler() {
    //    if (DiscordClient == null || Token != Config.Token) {
    //        await Run();
    //    }

    //    if (DiscordClient == null) {
    //        Logger.Error(new NullReferenceException("Discord client not setup yet!"));
    //        return;
    //    }

    //    if (Config.CommandChannel != null) {
    //        try {
    //            CommandChannel = await DiscordClient.GetChannelAsync(ulong.Parse(Config.CommandChannel));
    //        } catch (Exception e) {
    //            Logger.Error($"Failed to get command channel \"{Config.CommandChannel}\"");
    //            Logger.Error(e);
    //        }
    //    }

    //    if (Config.LogChannel != null) {
    //        try {
    //            LogChannel = await DiscordClient.GetChannelAsync(ulong.Parse(Config.LogChannel));
    //        } catch (Exception e) {
    //            Logger.Error($"Failed to get log channel \"{Config.LogChannel}\"");
    //            Logger.Error(e);
    //        }
    //    }
    //}

    //private static List<string> SplitMessage(string message, int maxSizePerElem = 2000)
    //{
    //    List<string> result = new List<string>();
    //    for (int i = 0; i < message.Length; i += maxSizePerElem) 
    //    {
    //        result.Add(message.Substring(i, message.Length - i < maxSizePerElem ? message.Length - i : maxSizePerElem));
    //    }
    //    return result;
    //}

    //private async void Log(string source, string level, string text, ConsoleColor _) {
    //    try {
    //        if (DiscordClient != null && LogChannel != null) {
    //            foreach (string mesg in SplitMessage(Logger.PrefixNewLines(text, $"{level} [{source}]"), 1994)) //room for 6 '`'
    //                await DiscordClient.SendMessageAsync(LogChannel, $"```{mesg}```");
    //        }
    //    } catch (Exception e) {
    //        // don't log again, it'll just stack overflow the server!
    //        if (Reconnecting) return; // skip if reconnecting
    //        await Console.Error.WriteLineAsync("Exception in discord logger");
    //        await Console.Error.WriteLineAsync(e.ToString());
    //    }
    //}

    //public async Task Run() {
    //    Token = Config.Token;
    //    DiscordClient?.Dispose();
    //    if (Config.Token == null) {
    //        DiscordClient = null;
    //        return;
    //    }

    //    try {
    //        DiscordClient = new DiscordClient(new DiscordConfiguration {
    //            Token = Config.Token,
    //            MinimumLogLevel = LogLevel.None
    //        });
    //        await DiscordClient.ConnectAsync(new DiscordActivity("Hide and Seek", ActivityType.Competing));
    //        SettingsLoadHandler();
    //        Logger.Info(
    //            $"Discord bot logged in as {DiscordClient.CurrentUser.Username}#{DiscordClient.CurrentUser.Discriminator}");
    //        Reconnecting = false;
    //        string mentionPrefix = $"{DiscordClient.CurrentUser.Mention}";
    //        DiscordClient.MessageCreated += async (_, args) => {
    //            if (args.Author.IsCurrent) return; //dont respond to commands from ourselves (prevent "sql-injection" esq attacks)
    //            //prevent commands via dm and non-public channels
    //            if (CommandChannel == null) {
    //                if (args.Channel is DiscordDmChannel)
    //                    return; //no dm'ing the bot allowed!
    //            }
    //            else if (args.Channel.Id != CommandChannel.Id && (LogChannel != null && args.Channel.Id != LogChannel.Id))
    //                return;
    //            //run command
    //            try {
    //                DiscordMessage msg = args.Message;
    //                string? resp = null;
    //                if (string.IsNullOrEmpty(Prefix)) {
    //                    await msg.Channel.TriggerTypingAsync();
    //                    resp = string.Join('\n', CommandHandler.GetResult(msg.Content).ReturnStrings);
    //                } else if (msg.Content.StartsWith(Prefix)) {
    //                    await msg.Channel.TriggerTypingAsync();
    //                    resp = string.Join('\n', CommandHandler.GetResult(msg.Content[Prefix.Length..]).ReturnStrings);
    //                } else if (msg.Content.StartsWith(mentionPrefix)) {
    //                    await msg.Channel.TriggerTypingAsync();
    //                    resp = string.Join('\n', CommandHandler.GetResult(msg.Content[mentionPrefix.Length..].TrimStart()).ReturnStrings);
    //                }
    //                if (resp != null)
    //                {
    //                    foreach (string mesg in SplitMessage(resp))
    //                        await msg.RespondAsync(mesg);
    //                }
    //            } catch (Exception e) {
    //                Logger.Error(e);
    //            }
    //        };
    //        DiscordClient.ClientErrored += (_, args) => {
    //            Logger.Error("Discord client caught an error in handler!");
    //            Logger.Error(args.Exception);
    //            return Task.CompletedTask;
    //        };
    //        DiscordClient.SocketErrored += (_, args) => {
    //            Logger.Error("This is probably that stupid bug again!");
    //            Logger.Error("Discord client caught an error on socket!");
    //            Logger.Error(args.Exception);
    //            return Task.CompletedTask;
    //        };
    //    } catch (Exception e) {
    //        Logger.Error("Exception occurred in discord runner!");
    //        Logger.Error(e);
    //    }
    //}
    #endregion
}
