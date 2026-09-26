using TinyNet.ActionResult;
using TinyNet.ActionResult.Results;
using TinyNet.Controllers;
using TinyNet.Http;
using TinyNet.Middlewares;

namespace TinyNet.Tests;

public class RoutingTests
{
    public class NewUserForm : IGetHandler
    {
        public Task<IActionResult> Get(HttpContext context) => Reply("form");
    }

    public class UserHandler : IGetHandler, IPostHandler
    {
        public Task<IActionResult> Get(HttpContext context) => Reply("get " + context.GetFromRoute("id"));
        public Task<IActionResult> Post(HttpContext context) => Reply("post " + context.GetFromRoute("id"));
    }

    public class NumberHandler : IGetHandler
    {
        public Task<IActionResult> Get(HttpContext context)
            => context.GetFromRoute<int>("id") is { } id
                ? Reply("number " + id)
                : Task.FromResult<IActionResult>(new BadRequest("id must be a number"));
    }

    public class TraceHandler : IGetHandler, IPostHandler
    {
        public Task<IActionResult> Get(HttpContext context) => Reply(Trace(context));
        public Task<IActionResult> Post(HttpContext context) => Reply(Trace(context));
    }

    public abstract class TraceFilter(string name) : IMiddleware
    {
        public Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var trace = Trace(context);
            context.Request.Headers["X-Trace"] = trace.Length == 0 ? name : $"{trace},{name}";
            return next(context);
        }
    }

    public class OuterFilter() : TraceFilter("outer");
    public class InnerFilter() : TraceFilter("inner");
    public class EndpointFilter() : TraceFilter("endpoint");

    [Theory]
    [InlineData("POST", "/users/new", 200, "post new")]
    [InlineData("GET", "/users/new", 200, "form")]
    [InlineData("GET", "/users/42", 200, "get 42")]
    [InlineData("PUT", "/users/new", 405, "")]
    [InlineData("GET", "/missing", 404, "")]
    [InlineData("GET", "/groups/7/numbers", 200, "number 7")]
    [InlineData("GET", "/groups/abc/numbers", 400, "id")]
    public async Task Request_IsDispatchedByPathAndMethod(string method, string target, int status, string body)
    {
        await using var server = TestServer.Start(routes =>
        {
            routes.AddGroup("/users/new")
                .AddGetHandler<NewUserForm>();
            routes.AddGroup("/users/{id}")
                .AddGetHandler<UserHandler>()
                .AddPostHandler<UserHandler>();
            routes.AddGroup("/groups/{id}")
                .AddGroup("/numbers")
                .AddGetHandler<NumberHandler>();
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
            var inner = outer.AddGroup("/b/c")
                .AddGetHandler<TraceHandler>()
                .AddPostHandler<TraceHandler>(endpoint => endpoint.AddFilter<EndpointFilter>());
            inner.AddFilter<InnerFilter>();
        });

        var response = await server.SendAsync(method, "/a/b/c");

        Assert.Equal(200, response.Status);
        Assert.Equal($"\"{trace}\"", response.Body);
    }

    [Fact]
    public async Task MalformedJsonBody_IsRejectedBeforeController()
    {
        await using var server = TestServer.Start(routes =>
            routes.AddGroup("/users/{id}").AddPostHandler<UserHandler>());

        var response = await server.SendAsync("POST", "/users/1", "{broken");

        Assert.Equal(400, response.Status);
        Assert.DoesNotContain("post", response.Body);
    }

    private static string Trace(HttpContext context)
        => context.GetFromHeader("X-Trace") ?? "";

    private static Task<IActionResult> Reply(string text)
        => Task.FromResult<IActionResult>(new Ok(text));
}