using System.Collections;

namespace TinyNet.Configurations.Provider;

public class EnvironmentConfigurationProvider : IConfigurationProvider
{
    private readonly string _prefix;
    private Dictionary<string, string> _data = new();

    public EnvironmentConfigurationProvider(string prefix = null) => _prefix = prefix;

    public void Load()
    {
        _data = Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .Where(e => _prefix == null || ((string)e.Key).StartsWith(_prefix))
            .ToDictionary(
                e => ((string)e.Key).Replace("__", ":"), 
                e => (string)e.Value);
    }
    
    public bool TryGet(string key, out string value) 
        => _data.TryGetValue(key, out value);

    public IEnumerable<string> GetChildKeys() 
        => _data.Keys;
    
}