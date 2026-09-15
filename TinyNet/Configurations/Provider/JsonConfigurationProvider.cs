using System.Text.Json;

namespace TinyNet.Configurations.Provider;

public class JsonConfigurationProvider : IConfigurationProvider
{
    private readonly string _filePath;
    private readonly bool _optional;
    private Dictionary<string, string> _data = new();

    public JsonConfigurationProvider(string filePath, bool optional = false)
    {
        _filePath = filePath;
        _optional = optional;
    }

    public void Load()
    {
        if (!File.Exists(_filePath))
        {
            if (_optional)
                return;
            throw new FileNotFoundException(
                $"Configuration file '{_filePath}' not found. Pass optional: true to ignore it.", _filePath);
        }

        string json;
        try
        {
            json = File.ReadAllText(_filePath);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"Cannot read configuration file '{_filePath}': {ex.Message}", ex);
        }

        Dictionary<string, object> jsonDict;
        try
        {
            jsonDict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Configuration file '{_filePath}' is not valid JSON: {ex.Message}", ex);
        }

        _data = jsonDict == null ? new() : FlattenDictionary(jsonDict);
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