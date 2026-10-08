using TinyNet.Configurations.Provider;

namespace TinyNet.Configurations;

public class ConfigurationBuilder
{
    private readonly List<IConfigurationProvider> _providers = new();

    private readonly MemoryConfigurationProvider _defaults = new();

    private bool _built;

    public ConfigurationBuilder AddDefault(string key, string value)
    {
        EnsureNotBuilt();
        _defaults.Set(key, value);
        return this;
    }

    public ConfigurationBuilder AddDefaults(IEnumerable<KeyValuePair<string, string>> values)
    {
        EnsureNotBuilt();
        foreach (var value in values)
            _defaults.Set(value.Key, value.Value);
        return this;
    }

    public ConfigurationBuilder AddJsonFile(string path, bool optional = false)
    {
        EnsureNotBuilt();
        _providers.Add(new JsonConfigurationProvider(path, optional));
        return this;
    }

    public ConfigurationBuilder AddEnvironmentVariables(string prefix = null)
    {
        EnsureNotBuilt();
        _providers.Add(new EnvironmentConfigurationProvider(prefix));
        return this;
    }

    public IConfiguration Build()
    {
        EnsureNotBuilt();
        _built = true;
        var providers = new List<IConfigurationProvider>(_providers.Count + 1) { _defaults };
        providers.AddRange(_providers);
        return new ConfigurationRoot(providers);
    }

    private void EnsureNotBuilt()
    {
        if (_built)
            throw new InvalidOperationException("Configuration cannot be changed after Build()");
    }
}