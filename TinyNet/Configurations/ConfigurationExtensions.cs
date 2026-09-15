using System.Globalization;

namespace TinyNet.Configurations;

public static class ConfigurationExtensions
{
    public static T GetValue<T>(this IConfiguration configuration, string key) where T : IParsable<T>
    {
        var raw = configuration[key];
        if (raw == null)
            throw new InvalidOperationException($"Configuration key '{key}' is not set and has no default");
        return Parse<T>(key, raw);
    }

    public static T GetValue<T>(this IConfiguration configuration, string key, T fallback) where T : IParsable<T>
    {
        var raw = configuration[key];
        return raw == null ? fallback : Parse<T>(key, raw);
    }

    private static T Parse<T>(string key, string raw) where T : IParsable<T>
    {
        if (!T.TryParse(raw, CultureInfo.InvariantCulture, out var value))
            throw new InvalidOperationException(
                $"Configuration key '{key}' has value '{raw}', which is not a valid {typeof(T).Name}");
        return value;
    }
}