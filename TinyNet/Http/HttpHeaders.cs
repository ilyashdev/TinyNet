using System.Diagnostics;

namespace TinyNet.Http;
public sealed class HttpHeaders                                                                                                                                                                                                                  
{                                                                                                                                                                                                                                                
    private readonly Dictionary<string, List<string>> _values = new(StringComparer.OrdinalIgnoreCase);                                                                                                                                           
    
    public HttpHeaders Add(string key, string value)                                                                                                                                                                                             
    {                                                                                                                                                                                                                                            
        if (HasControlChars(key) || HasControlChars(value))                                                                                                                                                                                      
            throw new ArgumentException($"Header '{key}' contains CR, LF or NUL");                                                                                                                                                               
        if (!_values.TryGetValue(key, out var values))                                                                                                                                                                                           
            _values[key] = values = [];                                                                                                                                                                                                          
        values.Add(value);                                                                                                                                                                                                                       
        return this;                                                                                                                                                                                                                             
    }                                                                                                                                                                                                                                            
    
    public string? GetOrNull(string key) => _values.TryGetValue(key, out var v) ? string.Join(", ", v) : null;                                                                                                                                         
    internal IEnumerable<KeyValuePair<string, List<string>>> All => _values;                                                                                                                                                                     
    
    private static bool HasControlChars(string s) => s.AsSpan().IndexOfAny('\r', '\n', '\0') >= 0;                                                                                                                                               
} 