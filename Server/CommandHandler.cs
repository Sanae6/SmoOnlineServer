namespace Server;

public static class CommandHandler {
    public delegate Response Handler(string[] args);

    public static Dictionary<string, Handler> Handlers = new Dictionary<string, Handler>();

    static CommandHandler() {
        RegisterCommand("help", _ => $"Valid commands: {string.Join(", ", Handlers.Keys)}");
    }

    public static void RegisterCommand(string name, Handler handler) {
        Handlers[name] = handler;
    }

    public static Response GetResult(string input) {
        try {
            string[] args = input.Split(' ');
            if (args.Length == 0) return "No command entered, see help command for valid commands";
            string commandName = args[0];
            return Handlers.TryGetValue(commandName, out Handler? handler) ? handler(args[1..]) : $"Invalid command {args[0]}, see help command for valid commands";
        }
        catch (Exception e) {
            return $"An error occured while trying to process your command: {e}";
        }
    }

    public class Response {
        public string[] ReturnStrings = null!;
        private Response(){}

        public static implicit operator Response(string value) => new Response {
            ReturnStrings = value.Split('\n')
        };
        public static implicit operator Response(string[] values) => new Response {
            ReturnStrings = values
        };
    }
}