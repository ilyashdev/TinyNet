namespace TinyNet.Http;

public static class HttpMethod
{
private static List<string> _allowMethod = new List<string>()
{
    "GET",
    "POST",
    "DELETE",
    "HEAD",
    "OPTIONS",
    "PATCH"
};
public static bool IsAllowed(string method) => _allowMethod.Contains(method);

}