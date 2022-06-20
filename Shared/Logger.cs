using System.Text;

namespace Shared;

public class Logger {
    public Logger(string name) {
        Name = name;
    }

    public string Name { get; set; }

    public void Info(string text) => Handler?.Invoke(Name, "Info", text, ConsoleColor.White);

    public void Warn(string text) => Handler?.Invoke(Name, "Warn", text, ConsoleColor.Yellow);

    public void Error(string text) => Handler?.Invoke(Name, "Error", text, ConsoleColor.Red);

    public void Error(Exception error) => Error(error.ToString());

    public static string PrefixNewLines(string text, string prefix) {
        StringBuilder builder = new StringBuilder();
        foreach (string str in text.Split('\n'))
            builder
                .Append(prefix)
                .Append(' ')
                .AppendLine(str);
        return builder.ToString();
    }

    public delegate void LogHandler(string source, string level, string text, ConsoleColor color);

    private static LogHandler? Handler;
    public static void AddLogHandler(LogHandler handler) => Handler += handler;

    static Logger() {
        AddLogHandler((source, level, text, color) => {
            DateTime logtime = DateTime.Now;
            Console.ForegroundColor = color;
            Console.Write(PrefixNewLines(text, $"{{{logtime}}} {level} [{source}]"));
        });
    }
}