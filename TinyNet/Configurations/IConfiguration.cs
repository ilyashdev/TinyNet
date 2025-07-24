namespace TinyNet.Configurations;
public interface IConfiguration
{
    string this[string key] { get; }
    IConfigurationSection GetSection(string sectionKey);
}

