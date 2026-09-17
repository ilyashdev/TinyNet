using System.Text.Json.Nodes;
using TinyNet.Controllers;
using TinyNet.DI;
using TinyNet.Http;

namespace TinyNet.Tests;

public class ParameterBindingTests
{
    [Fact]
    public async Task FromQuery_StringParameter_IsBound()
    {
        var request = new HttpRequest(
            "GET", "/query-string", new(), new() { ["term"] = "hello" }, null);

        var response = await DispatchAsync(request);

        Assert.Equal(200, response.StatusCode);
        Assert.Contains("hello", response.Body);
    }

    [Fact]
    public async Task FromBody_WithoutBody_ReturnsBadRequest()
    {
        var request = new HttpRequest("POST", "/body", new(), new(), null);

        var response = await DispatchAsync(request);

        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task FromBody_WithBody_IsBound()
    {
        var body = new JsonObject { ["name"] = "Bob" };
        var request = new HttpRequest("POST", "/body", new(), new(), body);

        var response = await DispatchAsync(request);

        Assert.Equal(200, response.StatusCode);
        Assert.Contains("Bob", response.Body);
    }

    private static async Task<HttpResponse> DispatchAsync(HttpRequest request)
    {
        var container = new DIContainer();
        var handler = new ControllerHandler(container);
        handler.InitControllers();

        using var scope = new DIScope();
        var context = new HttpContext(request);
        await handler.Handle(context, scope);

        return context.Response!;
    }
}