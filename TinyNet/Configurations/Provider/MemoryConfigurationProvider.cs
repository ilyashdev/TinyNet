namespace TinyNet.Configurations.Provider;

public class MemoryConfigurationProvider : IConfigurationProvider
{
    private readonly Dictionary<string, string> _data = new(StringComparer.OrdinalIgnoreCase);

    public MemoryConfigurationProvider()
    {
    }

    public MemoryConfigurationProvider(IEnumerable<KeyValuePair<string, string>> values)
    {
        foreach (var value in values)
            Set(value.Key, value.Value);
    }

    public void Set(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Configuration key cannot be empty", nameof(key));
        _data[key] = value;
    }

    public void Load()
    {
    }

    public bool TryGet(string key, out string value)
        => _data.TryGetValue(key, out value);

    public IEnumerable<string> GetChildKeys()
        => _data.Keys;
}