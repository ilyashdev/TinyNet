namespace TinyNet.Exceptions;

public enum RequestValueSource
{
    Route,
    Query
}

public sealed class RequestValueException : RequestException
{
    public RequestValueSource Source { get; }
    public string Key { get; }
    public string RawValue { get; }
    public Type TargetType { get; }

    internal RequestValueException(RequestValueSource source, string key, string rawValue, Type targetType)
        : base($"{source} parameter '{key}' must be {targetType.Name}", 400)
    {
        Source = source;
        Key = key;
        RawValue = rawValue;
        TargetType = targetType;
    }
}