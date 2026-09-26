# TinyNet

An HTTP web framework written from scratch in C# on .NET 10.

[Русская версия](README.ru.md)

---

## Status

**Works**

- Fluent application builder
- Dependency injection with singleton, scoped and transient lifetimes
- Lifetime validation at startup: a scoped dependency inside a singleton fails the build
- Explicit route tree with groups, nested groups and templates such as `/users/{id}`
- Controllers as plain classes implementing `IGetHandler`, `IPostHandler`, `IPutHandler`,
  `IPatchHandler`, `IDeleteHandler`
- Global middleware, and filters on route groups and on single endpoints
- Automatic `404` for an unknown path and `405` for a known path with another method
- Reading route, query, header and JSON body values from `HttpContext`
- Configuration from defaults, JSON and environment variables
- Keep-alive connections, including pipelined requests
- Request reading with fixed-length and chunked bodies
- Configurable request limits, answered with `413`, `408` and `400`; a malformed JSON body is
  one of them
- Admission control: a bounded queue and a `503` when it is full

**In progress**

- Static files — the handler is being rewritten as a middleware and is not connected now
- Request and response bodies as streams — a JSON body is parsed up front today
- `HEAD`, `OPTIONS` and the `Allow` header on `405`
- Concurrency: a worker owns a connection for its whole life. Under keep-alive a busy client
  keeps its worker, and queued connections can wait until it leaves (see
  [load tests](TinyNet.K6Bench/README.md)). The fix is to read requests on the accepting side
  and hand workers requests instead of connections

---

## Quick start

```csharp
var builder = new AppBuilder();

builder.AddJsonConfig("config.json");
builder.AddEnvironmentVariables("TINYNET_");

builder.Services.AddSingleton<MyService>();
builder.RegisterMiddleware<LoggingMiddleware>();

builder.Routes
    .AddGroup("/hello")
    .AddGetHandler<HelloController>();

var app = builder.Build();
await app.Run();
```

```csharp
public class HelloController : IGetHandler
{
    private readonly MyService _service;

    public HelloController(MyService service) => _service = service;

    public Task<IActionResult> Get(HttpContext context)
        => Task.FromResult<IActionResult>(new Ok(new { message = "Hello, World!" }));
}
```

```json
{
  "Server": { "Port": 5000 }
}
```

A complete application that uses every feature is in [`TinyNet.Example`](TinyNet.Example/README.md).

---

## Solution layout

| Project | Purpose |
|---|---|
| `TinyNet` | The framework |
| [`TinyNet.Example`](TinyNet.Example/README.md) | Example application using every framework feature |
| [`TinyNet.Tests`](TinyNet.Tests/README.md) | Test suite |
| [`TinyNet.K6Bench`](TinyNet.K6Bench/README.md) | Load simulators and k6 scenarios |

---

## Architecture

```
AppBuilder
    ├── DIContainer
    ├── ConfigurationBuilder
    ├── Routes (GroupRoute)
    └── MiddlewarePipeline
            │
            ▼
    WebApplication
            ├── accept thread "tinynet-accept"
            │       │
            │       ▼
            ├── Channel<NetClient>   — bounded; full ⇒ 503
            │       │
            │       ▼
            └── worker pool          — N = Server:MaxConcurrentRequests
                    │
                    ▼
            global middleware → router → group and endpoint filters → controller → IActionResult
```

`AppBuilder.Build()` does all the work up front: it walks the route tree, registers
controllers and filters in DI, builds the router, validates lifetimes, and composes every
chain of filters. A mistake in any of them fails the build, not the first request.

A request travels like this:

1. A dedicated thread accepts the connection and writes it to a bounded channel. When the
   channel is full that thread answers `503` itself and closes the connection.
2. A worker takes the connection and keeps it until it ends, opening a fresh `DIScope` for
   every request on it.
3. The request is read under `HttpLimits`, which yields `413`, `408` or `400` when a limit
   is exceeded or the request is malformed — including a body sent as `application/json` that
   is not a JSON object. Bytes read past the end of one request stay buffered for the next.
4. Global middleware runs, then the router finds the endpoint by path and method: `404` when
   the path is unknown, `405` when the path exists without that method.
5. The filters of the endpoint's groups run from the outermost inwards, then the endpoint's own
   filters, then the controller. Its `IActionResult` fills the `HttpResponse`, which is written
   to the socket.
6. The connection is reused until the client asks to close, `Server:KeepAliveMax` requests
   have been served, or it sits idle for `Server:KeepAliveTimeout` between requests.

Because a worker owns a connection rather than a request, `Server:MaxConcurrentRequests` is
in practice the number of simultaneous *clients*. Connections beyond it wait in the queue and
are not read at all, so plan capacity by connections, not by request rate.

---

## Dependency injection

```csharp
builder.Services.AddSingleton<IMyService, MyService>();
builder.Services.AddScoped<IMyService, MyService>();
builder.Services.AddTransient<IMyService, MyService>();
builder.Services.AddSingleton<MyService>();
builder.Services.AddInstance<IMyService>(existingInstance);
```

Dependencies arrive through the constructor; the constructor with the most parameters is
chosen. Constructors are compiled into delegates on first use and cached. Cyclic dependencies
throw `InvalidOperationException` at resolution time.

Lifetimes of framework parts are fixed by the framework, not configured:

| Part | Lifetime | Registered by |
|---|---|---|
| Controller | transient, one per request | the framework, from the routes |
| Filter | singleton | the framework, from `AddFilter<T>()` |
| Middleware | singleton | `RegisterMiddleware<T>()` |

Middleware and filters are shared by parallel requests, so they must be thread-safe and may
take only singletons in their constructor. Per-request services are taken inside
`InvokeAsync` with `context.GetService<T>()`.

`AppBuilder.Build()` validates lifetimes before the server starts. A singleton that depends
on a scoped service — directly or through transients — captures it for the life of the
process, so the build fails with the whole chain named:

```
Captive dependency: Reports (Singleton) -> ReportBuilder (Transient) -> DbSession (Scoped)
```

A dependency whose type is not registered is skipped by the check, and still throws at
resolution time.

---

## Routes and controllers

Routes are declared in one place, the route tree on `builder.Routes`. It is the only way to
declare a route. A path is built from groups only; a handler is attached to the end of its
group and returns that group, so calls chain:

```csharp
var users = builder.Routes
    .AddGroup("/users")
    .AddFilter<AuthFilter>()
    .AddGetHandler<UsersController>()
    .AddPostHandler<UsersController>(endpoint => endpoint.AddFilter<AdminFilter>());

users.AddGroup("{id}")
    .AddGetHandler<UserController>()
    .AddDeleteHandler<UserController>(endpoint => endpoint.AddFilter<AdminFilter>());
```

A controller is any class implementing handler interfaces. `AddGetHandler<C>()` requires
`C : IGetHandler`, so the compiler checks the connection between a route and a method:

```csharp
public class UserController : IGetHandler, IDeleteHandler
{
    public Task<IActionResult> Get(HttpContext context) { ... }
    public Task<IActionResult> Delete(HttpContext context) { ... }
}
```

- A group filter applies to every endpoint inside the group, nested groups included. Filters
  run from the outer group to the inner one, then the endpoint's own filters; within a level —
  in the order they were added. A filter added to a group after its handlers still applies.
- A group can be built separately and attached with `AddGroup(group)`; a group has one parent.
- Matching walks the path segment by segment. A literal segment wins over a parameter, and a
  dead-end branch is backtracked, so `/users/me` wins over `/users/{id}` while
  `POST /users/me` still reaches `POST /users/{id}` when `/users/me` has only `GET`. Segments
  are compared case-insensitively, a trailing slash is ignored, and each segment is
  percent-decoded after the split, so `%2F` cannot smuggle in a separator.
- `Build()` fails on a duplicate method and path, a filter applied twice on the way to an
  endpoint, two different parameter names in the same position, a mixed segment like `v{id}`,
  and a group attached to a second parent. A second handler of the same method on one group
  fails immediately.

---

## Reading request data

A controller reads everything from `HttpContext`. Nothing is bound to method parameters and
nothing throws — `null` means the value is missing or cannot be parsed, and the controller
decides how to answer:

```csharp
var id = context.GetFromRoute<int>("id");            // int?
if (id is null)
    return new BadRequest("id must be a number");

var take = context.GetFromQuery<int>("take") ?? 10;  // int?
var key = context.GetFromHeader("X-Api-Key");        // string?

var input = await context.ReadFromBodyAsync<NoteInput>();   // NoteInput?
if (input is null)
    return new BadRequest("Body must be a note");

var service = context.GetService<MyScopedService>();
```

- `GetFromRoute`, `GetFromQuery` and `GetFromHeader` return `string?` without a type argument
  and `T?` for any `IParsable<T>` value type. Values are parsed with the invariant culture.
- `GetFromQuery<int>("take") ?? 10` treats `?take=many` as missing. When garbage has to be
  told apart from absence, check `context.Request.Query.ContainsKey("take")`.
- A body is parsed as JSON only when `Content-Type` is `application/json` or ends with
  `+json`. A malformed body, or one that is not a JSON object, is answered `400` while the
  request is read, before any middleware. Any other body is not parsed and
  `ReadFromBodyAsync` returns `null`.
- JSON property names are case-insensitive, both when reading bodies and in responses.
- The raw dictionaries `context.Request.Route`, `.Query` and `.Headers` stay available.

---

## Middleware and filters

Middleware and filters implement the same interface:

```csharp
public class LoggingMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        Console.WriteLine($"→ {context.Request.Method} {context.Request.Url}");
        await next(context);
    }
}
```

`RegisterMiddleware<T>()` runs it for every request, before routing, in registration order.
`AddFilter<T>()` on a group or an endpoint runs it only for that part of the route tree.
Code after `await next(context)` runs once the response is filled. A middleware or filter that
does not call `next` ends the request there — set `context.Response` with a result first:

```csharp
new BaseResult(401, "Missing X-Api-Key").ExecuteResult(context);
return Task.CompletedTask;
```

A `400`, `408` or `413` produced while reading the request happens before the pipeline, so
middleware does not see those responses.

---

## Configuration

Sources are read in the order defaults → `config.json` → environment variables, and a later
source wins. Defaults always come first, whatever order they were registered in.

```csharp
builder.AddDefault("Server:Port", "8080");
builder.AddJsonConfig("config.json", optional: false);
builder.AddEnvironmentVariables("TINYNET_");
```

Nested JSON keys are flattened with `:`, and `__` in an environment variable means the same
separator: `TINYNET_Server__Port=5000` becomes `Server:Port`. Values are read through
`IConfiguration`, which is available in any class resolved from DI:

```csharp
var port = configuration.GetValue<int>("Server:Port");
var key = configuration["Example:ApiKey"];
```

### Framework keys

| Key | Default | Meaning |
|---|---|---|
| `Server:Port` | `5000` | listening port; `0` lets the OS choose |
| `Server:MaxConcurrentRequests` | `256` | worker count, and thus simultaneous connections |
| `Server:MaxQueuedConnections` | `1024` | queue capacity; full ⇒ `503` |
| `Server:MaxHeadBytes` | `16384` | request head limit ⇒ `413` |
| `Server:MaxBodyBytes` | `8388608` | request body limit ⇒ `413` |
| `Server:ReceiveBufferSize` | `8192` | socket read buffer, one per connection |
| `Server:ReadTimeoutSeconds` | `15` | time to send a complete request ⇒ `408` |
| `Server:KeepAliveMax` | `1000` | requests served on one connection before it is closed |
| `Server:KeepAliveTimeout` | `5` | seconds a connection may sit idle between requests |
| `WebRoot:Path` | `./WebRoot` | static file root; unused while static files are disconnected |

`KeepAliveTimeout` limits the pause *between* requests, not the life of a connection: a client
that keeps sending keeps its worker for up to `KeepAliveMax` requests. It is deliberately
short, because an idle connection holds a worker. Behind a reverse proxy that keeps an
upstream pool it has to be raised above the proxy's own idle timeout — otherwise the server
closes a pooled connection under the proxy and the client sees `502`.

---

## Action results

| Class | Status | Body |
|---|---|---|
| `Ok` | 200 | optional JSON |
| `BadRequest` | 400 | optional JSON |
| `NotFound` | 404 | optional JSON |
| `InternalError` | 500 | optional JSON |
| `HtmlView` | 200 | HTML |
| `Media` | 200 | text or bytes with an explicit content type |
| `BaseResult` | any | optional JSON |

```csharp
return new Ok(new { id = 1 });
return new BaseResult(201, created);
return new HtmlView("<h1>Hello</h1>");
return new Media(bytes, "image/png");
```

A new result inherits from `BaseResult` for a JSON body, or from `ActionResult` to fill the
response itself.

---

## Static files

Not served at the moment: the static file handler is being rewritten as a middleware. Its
path check is kept and covered by tests — a path leading outside the web root is rejected.

---

## License

Apache License 2.0 — see [LICENSE](LICENSE).