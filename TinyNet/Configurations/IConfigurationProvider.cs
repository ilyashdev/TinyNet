namespace TinyNet.Configurations;

public interface IConfigurationProvider
{
    bool TryGet(string key, out string value);
    void Load();
    IEnumerable<string> GetChildKeys();
}
