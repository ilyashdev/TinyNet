namespace TinyNet.Configurations;

public class ConfigurationRoot : IConfiguration
{
    private readonly List<IConfigurationProvider> _providers;

    public ConfigurationRoot(IEnumerable<IConfigurationProvider> providers)
    {
        _providers = providers.ToList();
        foreach (var provider in _providers)
        {
            provider.Load();
        }
    }

    public string this[string key]
    {
        get
        {
            foreach (var provider in _providers.AsEnumerable().Reverse())
            {
                if (provider.TryGet(key, out var value))
                {
                    return value;
                }
            }
            return null;
        }
    }

    public IConfigurationSection GetSection(string key) 
        => new ConfigurationSection(this, key);
}

