using System.Text;

namespace TinyNet.Example.Pages;

public static class Form
{
    public static Dictionary<string, string> Parse(ReadOnlyMemory<byte> body)
    {
        var fields = new Dictionary<string, string>();
        foreach (var pair in Encoding.UTF8.GetString(body.Span).Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            fields.TryAdd(Decode(eq < 0 ? pair : pair[..eq]), eq < 0 ? "" : Decode(pair[(eq + 1)..]));
        }

        return fields;
    }

    private static string Decode(string s) => Uri.UnescapeDataString(s.Replace('+', ' '));
}