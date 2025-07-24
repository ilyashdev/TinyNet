using TinyNet.Configurations.Provider;

namespace TinyNet.Configurations;

public class ConfigurationBuilder
{
    private readonly List<IConfigurationProvider> _providers = new();

    public ConfigurationBuilder AddJsonFile(string path)
    {
        _providers.Add(new JsonConfigurationProvider(path));
        return this;
    }

    public ConfigurationBuilder AddEnvironmentVariables(string prefix = null)
    {
        _providers.Add(new EnvironmentConfigurationProvider(prefix));
        return this;
    }

    public IConfiguration Build() => new ConfigurationRoot(_providers);
}