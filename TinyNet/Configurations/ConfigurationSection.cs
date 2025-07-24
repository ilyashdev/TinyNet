namespace TinyNet.Configurations;

public class ConfigurationSection : IConfigurationSection
{
    private readonly IConfiguration _root;
    
    public ConfigurationSection(IConfiguration root, string key)
    {
        _root = root;
        Key = key;
    }

    public string Key { get; }
    public string Value => _root[Key];
    
    public string this[string key] => _root[$"{Key}:{key}"];
    
    public IConfigurationSection GetSection(string sectionKey) 
        => new ConfigurationSection(this, sectionKey);
}