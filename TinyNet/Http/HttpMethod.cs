namespace TinyNet.Http;

public static class HttpMethod
{
private static readonly HashSet<string>  AllowMethod = new()
{
    "GET",
    "POST",
    "DELETE",
    "HEAD",
    "OPTIONS",
    "PATCH",
    "PUT"
};
public static bool IsAllowed(string method) => AllowMethod.Contains(method);

}