namespace TinyNet.Configurations;

public interface IConfigurationSection : IConfiguration
{
    string Key { get; }
    string Value { get; }
}