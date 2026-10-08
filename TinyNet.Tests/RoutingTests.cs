using TinyNet.Http;
using TinyNet.Middlewares;

namespace TinyNet.Tests;

public class RoutingTests
{
    public record Person(string Name);

    public class Trace
    {
        public List<string> Names { get; } = new();
    }

    public abstract class TraceFilter(string name) : IMiddleware
    {
        public Task<HttpResponse> InvokeAsync(HttpRequest request, HttpContext context, RequestDelegate next)
        {
            context.GetService<Trace>().Names.Add(name);
            return next(request, context);
        }
    }

    public class OuterFilter() : TraceFilter("outer");

    public class InnerFilter() : TraceFilter("inner");

    public class EndpointFilter() : TraceFilter("endpoint");

    [Theory]
    [InlineData("POST", "/users/new", 200, "post new")]
    [InlineData("GET", "/users/new", 200, "form")]
    [InlineData("GET", "/users/42", 200, "get 42")]
    [InlineData("HEAD", "/users/42", 200, "")]
    [InlineData("PUT", "/users/new", 405, "")]
    [InlineData("GET", "/missing", 404, "")]
    [InlineData("GET", "/groups/7/numbers", 200, "number 7")]
    [InlineData("GET", "/groups/abc/numbers", 400, "id")]
    public async Task Request_IsDispatchedByPathAndMethod(string method, string target, int status, string body)
    {
        await using var server = TestServer.Start(routes =>
        {
            routes.AddGet("/users/new", (_, context) => Reply(context, "form"));
            routes.AddGroup("/users/{id}")
                .AddGet("/", (request, context) => Reply(context, "get " + request.GetFromRoute("id")))
                .AddPost("/", (request, context) => Reply(context, "post " + request.GetFromRoute("id")));
            routes.AddGroup("/groups/{id}")
                .AddGet("/numbers", (request, context) => Reply(context, "number " + request.GetFromRoute<int>("id")));
        });

        var response = await server.SendAsync(method, target);

        Assert.Equal(status, response.Status);
        Assert.Contains(body, response.Body);
    }

    [Theory]
    [InlineData("GET", "outer,inner")]
    [InlineData("POST", "outer,inner,endpoint")]
    public async Task Filters_RunFromOuterGroupToEndpoint(string method, string trace)
    {
        await using var server = TestServer.Start(routes =>
        {
            var outer = routes.AddGroup("/a").AddFilter<OuterFilter>();
            var inner = outer.AddGroup("/b")
                .AddGet("/c", Names)
                .AddPost("/c", Names, endpoint => endpoint.AddFilter<EndpointFilter>());
            inner.AddFilter<InnerFilter>();
        }, app => app.Services.AddScoped<Trace>());

        var response = await server.SendAsync(method, "/a/b/c");

        Assert.Equal(200, response.Status);
        Assert.Equal(trace, response.Body);
    }

    [Fact]
    public async Task MalformedJsonBody_IsAnswered400()
    {
        await using var server = TestServer.Start(routes =>
            routes.AddPost("/people", async (request, context) =>
                await Reply(context, "read " + (await request.ReadJsonAsync<Person>())?.Name)));

        var response = await server.SendAsync("POST", "/people", "{broken");

        Assert.Equal(400, response.Status);
        Assert.DoesNotContain("read", response.Body);
    }

    private static Task<HttpResponse> Names(HttpRequest request, HttpContext context)
        => Reply(context, string.Join(",", context.GetService<Trace>().Names));

    private static Task<HttpResponse> Reply(HttpContext context, string text)
        => Task.FromResult(context.Response().Text(text));
}
