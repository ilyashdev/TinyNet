using System.Buffers;

namespace TinyNet.Http;

public sealed class HttpHeaders
{
    private readonly Dictionary<string, List<string>> _values = new(StringComparer.OrdinalIgnoreCase);

    private static readonly SearchValues<char> TokenChars =
        SearchValues.Create("!#$%&'*+-.^_`|~0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz");

    private static readonly SearchValues<char> FieldValueChars =
        SearchValues.Create(string.Concat(Enumerable.Range(0, 256)
            .Where(c => c == '\t' || c is >= 0x20 and not 0x7F).Select(c => (char)c)));

    public HttpHeaders Add(string key, string value)
    {
        if (key.Length == 0 || key.AsSpan().ContainsAnyExcept(TokenChars))
            throw new ArgumentException($"Header name '{key}' is not a valid token.", nameof(key));
        if (value.AsSpan().ContainsAnyExcept(FieldValueChars))
            throw new ArgumentException(
                $"Header '{key}' value contains control characters or characters outside Latin-1.", nameof(value));
        if (!_values.TryGetValue(key, out var values))
            _values[key] = values = [];
        values.Add(value);
        return this;
    }

    public string? GetOrNull(string key) => _values.TryGetValue(key, out var v) ? string.Join(", ", v) : null;
    internal IEnumerable<KeyValuePair<string, List<string>>> All => _values;
}