using System.Text;
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

    public static void RegisterCommandAliases(Handler handler, params string[] names) {
        foreach (string name in names) {
            Handlers.Add(name, handler);
        }
    }

    /// <summary>
    /// Modified by <b>TheUbMunster</b>
    /// </summary>
    public static Response GetResult(string input)
    {
        try {
            string[] args = input.Split(' ');
            if (args.Length == 0) return "No command entered, see help command for valid commands";
            //this part is to allow single arguments that contain spaces (since the game seems to be able to handle usernames with spaces, we need to as well)
            List<string> newArgs = new List<string>();
            newArgs.Add(args[0]);
            for (int i = 1; i < args.Length; i++) {
                if (args[i].Length == 0) continue; //empty string (>1 whitespace between arguments).
                else if (args[i][0] == '\"') {
                    //concatenate args until a string ends with a quote
                    StringBuilder sb = new StringBuilder();
                    i--; //fix off-by-one issue
                    do
                    {
                        i++;
                        sb.Append(args[i] + " "); //add space back removed by the string.Split(' ')
                        if (i >= args.Length) {
                            return "Unmatching quotes, make sure that whenever quotes are used, another quote is present to close it (no action was performed).";
                        }
                    } while (args[i][^1] != '\"');
                    newArgs.Add(sb.ToString(1, sb.Length - 3)); //remove quotes and extra space at the end.
                }
                else
                {
                    newArgs.Add(args[i]);
                }
            }
            args = newArgs.ToArray();
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