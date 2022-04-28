using System.Text;

namespace Shared;

public class Logger {
    public Logger(string name) {
        Name = name;
    }

    public string Name { get; set; }

    public void Info(string text) {
        Console.ResetColor();
        Console.WriteLine(PrefixNewLines(text, $"Info [{Name}]"));
    }

    public void Warn(string text) {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(PrefixNewLines(text, $"Warn [{Name}]"));
    }

    public void Error(string text) {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(PrefixNewLines(text, $"Error [{Name}]"));
    }

    public void Error(Exception error) {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(PrefixNewLines(error.ToString(), $"Error [{Name}]"));
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