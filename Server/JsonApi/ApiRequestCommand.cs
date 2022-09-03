namespace Server.JsonApi;

public static class ApiRequestCommand {
    public static async Task<bool> Send(Context ctx) {
        if (!ctx.HasPermission("Commands")) {
            await Response.Send(ctx, "Error: Missing Commands permission.");
            return true;
        }

        if (!ApiRequestCommand.IsValid(ctx)) {
            return false;
        }

        string input = ctx.request!.GetData()!;
        string command = input.Split(" ")[0];

        // help doesn't need permissions and is invidualized to the token
        if (command == "help") {
            List<string> commands = new List<string>();
            commands.Add("help");
            commands.AddRange(
                ctx.Permissions
                    .Where(str => str.StartsWith("Commands/"))
                    .Select(str => str.Substring(9))
                    .Where(cmd => CommandHandler.Handlers.ContainsKey(cmd))
            );
            string commandsStr = string.Join(", ", commands);

            await Response.Send(ctx, $"Valid commands: {commandsStr}");
            return true;
        }

        // no permissions
        if (! ctx.HasPermission($"Commands/{command}")) {
            await Response.Send(ctx, $"Error: Missing Commands/{command} permission.");
            return true;
        }

        // execute command
        JsonApi.Logger.Info($"[Commands] " + input);
        await Response.Send(ctx, CommandHandler.GetResult(input));
        return true;
    }


    private static bool IsValid(Context ctx) {
        var command = ctx.request!.GetData();

        if (command == null) {
            JsonApi.Logger.Warn($"[Commands] Invalid request Data is \"null\" or missing and not a \"System.String\" from {ctx.socket.RemoteEndPoint}.");
            return false;
        }

        if (command.GetType() != typeof(string)) {
            JsonApi.Logger.Warn($"[Commands] Invalid request Data is \"{command.GetType()}\" and not a \"System.String\" from {ctx.socket.RemoteEndPoint}.");
            return false;
        }

        return true;
    }


    private class Response {
        public string[]? Output { get; set; }


        public static async Task Send(Context ctx, CommandHandler.Response response)
        {
            Response resp = new Response();
            resp.Output = response.ReturnStrings;
            await ctx.Send(resp);
        }
    }
}
