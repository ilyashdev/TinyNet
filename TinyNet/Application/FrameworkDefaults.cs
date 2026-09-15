namespace TinyNet.Application;

public static class FrameworkDefaults
{
    public const string ServerPort = "Server:Port";
    public const string WebRootPath = "WebRoot:Path";

    public static readonly KeyValuePair<string, string>[] All =
    [
        new(ServerPort, "5000"),
        new(WebRootPath, "./WebRoot")
    ];
}
