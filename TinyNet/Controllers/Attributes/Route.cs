namespace TinyNet.Controllers;

[AttributeUsage(AttributeTargets.Class)]
public class RouteAttribute(string url) : Attribute
{
    public string Url { get; } = url;
}