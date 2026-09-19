# TinyNet

An HTTP web framework written from scratch in C# on .NET 10.

[Русская версия](README.ru.md)

---

## Status

**Works**

- Fluent application builder
- Dependency injection with singleton, scoped and transient lifetimes
- Lifetime validation at startup: a scoped dependency inside a singleton fails the build
- Attribute-based routing with route templates such as `/users/{id}`
- Global middleware and per-controller filters
- Configuration from defaults, JSON and environment variables
- Keep-alive connections, including pipelined requests
- Request reading with fixed-length and chunked bodies
- Configurable request limits, answered with `413`, `408` and `400`
- Admission control: a bounded queue and a `503` when it is full
- Static files

**In progress**

- Route parameter binding — the router extracts `{id}`, but no `[FromRoute]` passes it to a method yet
- One handler per HTTP verb per controller — `[Route]` still lives on the class
- Request and response bodies as streams — a body is parsed as JSON today
- Query binding for types other than numbers
- Static files: caching, `ETag`, `304`, `HEAD`
- Concurrency: a worker owns a connection for its whole life, so the number of simultaneous
  clients is capped by `MaxConcurrentRequests + MaxQueuedConnections`

---

## Quick start

```csharp
var builder = new AppBuilder();

builder.AddJsonConfig("config.json");
builder.AddEnvironmentVariables("TINYNET_");

builder.Services.AddSingleton<MyService>();
builder.RegisterMiddleware<LoggingMiddleware>();

var app = builder.Build();
await app.Run();
```

```csharp
[Route("/hello")]
public class HelloController : Controller
{
    private readonly MyService _service;

    public HelloController(MyService service) => _service = service;

    [HttpMethod("GET")]
    public IActionResult Get() => new Ok(new { message = "Hello, World!" });
}
```

```json
{
  "Server": { "Port": 5000 },
  "WebRoot": { "Path": "./WebRoot" }
}
```

---

## Solution layout

| Project | Purpose |
|---|---|
| `TinyNet` | The framework |
| [`TinyNet.TestApp`](TinyNet.TestApp/README.md) | Sample application |
| [`TinyNet.Tests`](TinyNet.Tests/README.md) | Test suite |
| [`TinyNet.K6Bench`](TinyNet.K6Bench/README.md) | Load simulators and k6 scenarios |

---

## Architecture

```
AppBuilder
    ├── DIContainer
    ├── ConfigurationBuilder
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
            MiddlewarePipeline → ControllerHandler → IActionResult
```

A request travels like this:

1. A dedicated thread accepts the connection and writes it to a bounded channel. When the
   channel is full that thread answers `503` itself and closes the connection.
2. A worker takes the connection and keeps it until it ends, opening a fresh `DIScope` for
   every request on it.
3. The request is read under `HttpLimits`, which yields `413`, `408` or `400` when a limit
   is exceeded. Bytes read past the end of one request stay buffered for the next.
4. The middleware chain runs, then `ControllerHandler` resolves the controller, binds the
   parameters and invokes the method.
5. `IActionResult.ExecuteResult` fills the `HttpResponse`, which is written to the socket.
6. The connection is reused until the client asks to close, `Server:KeepAliveMax` requests
   have been served, or it sits idle for `Server:KeepAliveTimeout`.

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

Controllers and middleware get their dependencies
through constructor injection; the constructor with the most parameters is chosen. Cyclic
dependencies throw `InvalidOperationException` at resolution time. Constructors are compiled
into delegates on first use and cached, so resolution does not go through reflection.

`AppBuilder.Build()` validates lifetimes before the server starts. A singleton that depends
on a scoped service — directly or through transients — captures it for the life of the
process, so the build fails with the whole chain named:

```
Captive dependency: Reports (Singleton) -> ReportBuilder (Transient) -> DbSession (Scoped)
```

A dependency whose type is not registered is skipped by the check, and still throws at
resolution time.

---

## Controllers and routing

A controller inherits from `Controller`, carries `[Route]` and has methods marked with
`[HttpMethod]`. A controller handles one method per HTTP verb.

A route is a template: `/users/{id}` matches `/users/42`. Matching walks the path segment by
segment, preferring a literal segment over a parameter and backtracking when a literal branch
leads nowhere, so `/users/me` wins over `/users/{id}` while `/a/b/c` still matches
`/a/{id}/c`. Segments are compared case-insensitively, a trailing slash is ignored, and each
segment is percent-decoded after the split, so `%2F` cannot smuggle in a separator. Templates
are validated when the application starts: a duplicate route, a mixed segment like `v{id}`
and two different parameter names in the same position all throw there rather than at request
time. Path values are not bound to method parameters yet.

```csharp
[Route("/products")]
public class ProductsController : Controller
{
    [HttpMethod("GET")]
    public IActionResult GetAll() => new Ok(new[] { "first", "second" });

    [HttpMethod("POST")]
    public IActionResult Create([FromBody] string name) => new Ok(new { created = name });

    [HttpMethod("PUT")]
    public IActionResult Resize([FromQuery] int width) => new Ok(new { width });
}
```

`[FromBody]` takes the property of the JSON body named after the parameter. `[FromQuery]`
takes the query string value. `GET`, `POST`, `PUT`, `DELETE`, `PATCH`, `HEAD` and `OPTIONS`
are accepted. `[NotMapped]` keeps a controller out of routing.

---

## Middleware and filters

```csharp
public class LoggingMiddleware : Middleware
{
    public LoggingMiddleware(RequestDelegate next) : base(next) { }

    public override async Task InvokeAsync(HttpContext context)
    {
        Console.WriteLine($"→ {context.Request.Method} {context.Request.Url}");
        await _next(context);
    }
}
```

`RegisterMiddleware<T>()` runs the middleware for every request. `RegisterFilter<T>()` runs
it only for controllers marked with `[Filter(typeof(T))]`. Global middleware runs first,
then the filters of the matched controller, each group in registration order. A middleware
that does not call `_next` ends the chain.

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
var path = configuration["WebRoot:Path"];
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
| `WebRoot:Path` | `./WebRoot` | static file root, relative or absolute |

`KeepAliveTimeout` is deliberately short, because an idle connection holds a worker. Behind a
reverse proxy that keeps an upstream pool it has to be raised above the proxy's own idle
timeout — otherwise the server closes a pooled connection under the proxy and the client sees
`502`.

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

```csharp
return new Ok(new { id = 1 });
return new HtmlView("<h1>Hello</h1>");
return new Media(bytes, "image/png");
```

A new result inherits from `BaseResult` for a JSON body, or from `ActionResult` to fill the
response itself:

```csharp
public class Created : BaseResult
{
    public Created(object data) : base(201, data) { }
}
```

---

## Static files

A URL containing a `.` is served from the web root, which is resolved relative to the working
directory. Paths leading outside the root are rejected with `404`, as is a missing file.

`html`, `css`, `js`, `json`, `xml`, `jpeg`, `jpg`, `png`, `bmp`, `gif`, `tiff`, `tif`,
`webp`, `zip` and `rar` are served with their content type; anything else is served as
`application/octet-stream`.

---

## License

Apache License 2.0 — see [LICENSE](LICENSE).