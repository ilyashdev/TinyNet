using System.Text.Json;

namespace TinyNet.Configurations.Provider;

public class JsonConfigurationProvider : IConfigurationProvider
{
    private readonly string _filePath;
    private Dictionary<string, string> _data = new();

    public JsonConfigurationProvider(string filePath) => _filePath = filePath;

    public void Load()
    {
        var json = File.ReadAllText(_filePath);
        var jsonDict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        _data = FlattenDictionary(jsonDict);
    }

    private Dictionary<string, string> FlattenDictionary(
        Dictionary<string, object> dict, 
        string prefix = "")
    {
        var result = new Dictionary<string, string>();
        foreach (var kvp in dict)
        {
            var key = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}:{kvp.Key}";
            
            if (kvp.Value is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Object)
                {
                    var childDict = JsonSerializer.Deserialize<Dictionary<string, object>>(element.GetRawText());
                    foreach (var child in FlattenDictionary(childDict, key))
                    {
                        result.Add(child.Key, child.Value);
                    }
                }
                else
                {
                    result.Add(key, element.ToString());
                }
            }
        }
        return result;
    }

    public bool TryGet(string key, out string value) 
        => _data.TryGetValue(key, out value);

    public IEnumerable<string> GetChildKeys() 
        => _data.Keys;
}