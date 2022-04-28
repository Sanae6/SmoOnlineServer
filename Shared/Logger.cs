using System.Text;

namespace Shared;

public class Logger {
    public Logger(string name) {
        Name = name;
    }

    public string Name { get; set; }

    public void Info(string text) {
        Console.ResetColor();
        Console.Write(PrefixNewLines(text, $"Info [{Name}]"));
    }

    public void Warn(string text) {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write(PrefixNewLines(text, $"Warn [{Name}]"));
    }

    public void Error(string text) {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write(PrefixNewLines(text, $"Error [{Name}]"));
    }

    public void Error(Exception error) {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write(PrefixNewLines(error.ToString(), $"Error [{Name}]"));
    }

    private string PrefixNewLines(string text, string prefix) {
        StringBuilder builder = new StringBuilder();
        foreach (string str in text.Split('\n'))
            builder
                .Append(prefix)
                .Append(' ')
                .AppendLine(str);
        return builder.ToString();
    }
}