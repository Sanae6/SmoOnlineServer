using System.Text;

namespace Shared; 

public static class Extensions {
    public static string Hex(this Span<byte> span) {
        return span.ToArray().Hex();
    }
    
    public static string Hex(this IEnumerable<byte> array) => string.Join(' ', array.ToArray().Select(x => x.ToString("X2")));

    public static unsafe byte* Ptr(this Span<byte> span) {
        fixed (byte* data = span) return data;
    }

    public static string TrimNullTerm(this string text) {
        return text.Split('\0').FirstOrDefault() ?? "";
    }
}