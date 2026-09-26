using System.Globalization;

namespace TinyNet.Http;

internal static class RequestValues
{
    public static string? Find(this IReadOnlyDictionary<string, string> values, string name)
        => values.GetValueOrDefault(name);

    public static T? Find<T>(this IReadOnlyDictionary<string, string> values, string name) where T : struct, IParsable<T>
        => values.TryGetValue(name, out var raw) && T.TryParse(raw, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
}