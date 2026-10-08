# TinyNet

A bloat-free web framework for .NET 10, written from scratch around one idea: **explicit
control**. Everything that happens to a request is written in your code — not decided by
conventions, binding or scanning somewhere out of sight.

[Русская версия](README.ru.md)

---

## Why

Large frameworks do a lot for you, and much of it happens where you cannot see it: model
binding picks values, conventions pick handlers, scanning picks up classes. Several ways to
configure the same thing affect each other. When the result is wrong, you debug the framework
instead of your application.

TinyNet goes the other way:

- **Explicit control flow.** A request goes through the route tree, the filters you attached
  and the handler you wrote. A handler reads the values it needs from `HttpRequest` and returns
  an `HttpResponse` it built itself.
- **One way to do each thing.** Routes are declared in one tree in `Program.cs`, request data
  is read from one place, a response is built in one way. Two ways to do the same thing are
  treated as a bug.
- **No bloat.** Its own HTTP/1.1 server, DI container, configuration and pipeline in about
  3 300 lines, with no NuGet dependencies and no ASP.NET underneath.
- **The framework decides what is not worth choosing.** Lifetimes of handler classes, filters
  and middleware are fixed, and so is the concurrency model.
- **Checked before the first request.** The compiler checks that a route points to a method
  with the right signature; `Build()` checks routes, filters and service lifetimes before the
  server accepts a connection.
- **Visible overload.** Requests beyond the concurrency limit wait in a bounded queue; when it
  is full, clients get `503`, not a hanging or dropped connection.

---

## Status

**Works**

- Fluent application builder
- Dependency injection with singleton, scoped and transient lifetimes
- Lifetime validation at startup: a scoped dependency inside a singleton fails the build
- Explicit route tree with groups, nested groups and templates such as `/users/{id}`
- Handlers as delegates or as methods of plain classes created from DI per request
- Global middleware, and filters on route groups and on single endpoints
- `404` for an unknown path, `405` with `Allow` for a known path with another method, `HEAD`
  served by the `GET` endpoint
- Reading route, query, header values and the body (stream, buffer or JSON) from `HttpRequest`
- Response builder: status, headers, and a JSON, text, bytes, stream or empty body
- Configuration from defaults, JSON and environment variables
- HTTP/1.1 and HTTP/1.0 parsed strictly by RFC 9112; keep-alive and pipelined requests
- Fixed-length and chunked request bodies, `Expect: 100-continue`
- Request limits answered with `400`, `408`, `413` and `431`
- Concurrency: a slot per request, a bounded queue and `503` when it is full; a connection
  limit

**Not yet**

- Static files
- Client address (`RemoteEndPoint`, `X-Forwarded-For`)
- Logging abstraction: the framework writes to `Console`
- Send timeout and a time limit on graceful shutdown
- TLS, HTTP/2

---

## Quick start

```csharp
var builder = new AppBuilder();

builder.AddJsonConfig("config.json");
builder.AddEnvironmentVariables("TINYNET_");

builder.Services.AddSingleton<Greeter>();
builder.RegisterMiddleware<LoggingMiddleware>();

builder.Routes.AddGet("/health", (request, context) =>
    Task.FromResult(context.Response().Text("ok")));

builder.Routes
    .AddGroup("/hello")
    .AddGet<HelloHandler>("/{name}", h => h.Get);

var app = builder.Build();
await app.Run();
```

```csharp
public class HelloHandler
{
    private readonly Greeter _greeter;

    public HelloHandler(Greeter greeter) => _greeter = greeter;

    public Task<HttpResponse> Get(HttpRequest request, HttpContext context)
        => Task.FromResult(context.Response().Json(new { message = _greeter.Greet(request.GetFromRoute("name")) }));
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

| Project                                        | Purpose                                           |
|------------------------------------------------|---------------------------------------------------|
| `TinyNet`                                      | The framework                                     |
| [`TinyNet.Example`](TinyNet.Example/README.md) | Example application using every framework feature |
| [`TinyNet.Tests`](TinyNet.Tests/README.md)     | Test suite                                        |
| [`TinyNet.K6Bench`](TinyNet.K6Bench/README.md) | Load simulators and k6 scenarios                  |

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
            ├── accept loops         — Server:AcceptLoops; over Server:MaxConnections ⇒ 503, connection closed
            │       │
            │       ▼
            ├── one task per connection (Protocols/Http1)
            │       │
            │       ▼
            ├── request limit        — Server:MaxConcurrentRequests running,
            │                          Server:MaxQueuedRequests waiting ⇒ otherwise 503
            │       │
            │       ▼
            └── global middleware → router → group and endpoint filters → handler → HttpResponse
```

| Folder           | Contents                                                              | Depends on                     |
|------------------|-----------------------------------------------------------------------|--------------------------------|
| `Transport`      | sockets: `IConnectionListener`, `TcpConnectionListener`, `Connection` | —                              |
| `Http`           | the model: `HttpRequest`, `ResponseBuilder`, `HttpResponse`, `HttpContext` | `DI`, `Exceptions`        |
| `Protocols`      | HTTP/1.1 parser, body streams, response writer                        | `Http`, `Transport`, `DI`      |
| `Middlewares`    | `IMiddleware`, the pipeline                                           | `Http`, `DI`                   |
| `Routing`        | route tree, router, dispatcher                                        | `Http`, `Middlewares`, `DI`    |
| `Application`    | `AppBuilder` wires everything together, `WebApplication` hosts it     | everything                     |

The model knows nothing about sockets, and the transport knows nothing about HTTP.

`AppBuilder.Build()` does all the work up front: it walks the route tree, registers
handler classes and filters in DI, builds the router, validates lifetimes, and composes every
chain of filters. A mistake in any of them fails the build, not the first request.

A request travels like this:

1. One of `Server:AcceptLoops` accept loops takes the connection from the OS queue of
   `Server:ListenBacklog`. Above `Server:MaxConnections` open connections it answers `503` and
   closes the connection.
2. The connection gets its own task, which reads requests from it one after another and opens
   a fresh `DIScope` for each.
3. The head is parsed strictly by RFC 9112 under the head and body limits, which yield `400`,
   `408`, `413` or `431`. Bytes read past the end of one request stay buffered for the next.
4. The request takes one of `Server:MaxConcurrentRequests` slots. When all are busy it waits in
   a queue of up to `Server:MaxQueuedRequests` for at most `Server:RequestQueueTimeoutSeconds`;
   beyond that it is answered `503` with `Connection: close`.
5. Global middleware runs, then the router finds the endpoint by path and method: `404` when
   the path is unknown, `405` with `Allow` when the path exists without that method.
6. The filters of the endpoint's groups run from the outermost inwards, then the endpoint's own
   filters, then the handler. The slot is released once the handler has returned its
   `HttpResponse`, before the response is written to the socket.
7. The connection is reused until the client asks to close, `Server:KeepAliveMax` requests
   have been served, or it sits idle for `Server:KeepAliveTimeoutSeconds` between requests.

An idle keep-alive connection holds no slot: it only waits for bytes. `MaxConcurrentRequests`
limits the work that is running, `MaxConnections` limits memory and sockets.

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

| Part          | Lifetime                   | Registered by                        |
|---------------|----------------------------|--------------------------------------|
| Handler class | transient, one per request | the framework, from the routes       |
| Filter        | singleton                  | the framework, from `AddFilter<T>()` |
| Middleware    | singleton                  | `RegisterMiddleware<T>()`            |

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

## Routes and handlers

Routes are declared in one place, the route tree on `builder.Routes`. Every handler has the
same signature:

```csharp
delegate Task<HttpResponse> RequestDelegate(HttpRequest request, HttpContext context);
```

A handler is either a delegate, or a method of a class that the framework creates from DI for
each request. `h => h.Get` picks the method, and the compiler checks its signature:

```csharp
builder.Routes.AddGet("/health", (request, context) =>
    Task.FromResult(context.Response().Text("ok")));

var users = builder.Routes
    .AddGroup("/users")
    .AddFilter<AuthFilter>()
    .AddGet<UsersHandler>("/", h => h.List)
    .AddPost<UsersHandler>("/", h => h.Create, endpoint => endpoint.AddFilter<AdminFilter>())
    .AddGet<UsersHandler>("/{id}", h => h.Get)
    .AddDelete<UsersHandler>("/{id}", h => h.Delete, endpoint => endpoint.AddFilter<AdminFilter>());
```

`AddGet`, `AddPost`, `AddPut`, `AddPatch` and `AddDelete` return the group, so calls chain;
`AddGroup` returns the new group.

- A group filter applies to every endpoint inside the group, nested groups included. Filters
  run from the outer group to the inner one, then the endpoint's own filters; within a level —
  in the order they were added. A filter added to a group after its endpoints still applies.
- A group can be built separately and attached with `AddGroup(group)`; a group has one parent.
- A `HEAD` request is served by the `GET` endpoint of the same path, and the body is not sent.
- Matching walks the path segment by segment. A literal segment wins over a parameter, and a
  dead-end branch is backtracked, so `/users/me` wins over `/users/{id}` while
  `POST /users/me` still reaches `POST /users/{id}` when `/users/me` has only `GET`. Segments
  are compared case-insensitively, a trailing slash is ignored, and each segment is
  percent-decoded after the split, so `%2F` cannot smuggle in a separator.
- `Build()` fails on a duplicate method and path, a filter applied twice on the way to an
  endpoint, two different parameter names in the same position, a mixed segment like `v{id}`,
  and a group attached to a second parent. The route tree cannot be changed after `Build()`.

---

## Reading request data

A handler reads everything from `HttpRequest`. Nothing is bound to method parameters:

```csharp
var id = request.GetFromRoute<int>("id");            // int?, 400 when not a number
var take = request.GetFromQuery<int>("take") ?? 10;  // int?, null when missing
var key = request.GetFromHeaders("X-Api-Key");       // string?

var input = await request.ReadJsonAsync<NoteInput>(); // NoteInput?, 400 when not valid JSON
if (input is null)
    return context.Response().Status(StatusCodes.BadRequest).Text("Body must be a note");

var service = context.GetService<MyScopedService>();
```

- `GetFromRoute` and `GetFromQuery` return `string?` without a type argument and `T?` for any
  `IParsable<T>` value type. `null` means the value is missing. A value that is present but
  does not parse throws `RequestValueException`, answered `400`. Values are parsed with the
  invariant culture.
- `ReadJsonAsync<T>()` reads the body with the options from `builder.Json(...)`
  (`JsonSerializerDefaults.Web` by default: case-insensitive names, camelCase output). A body
  that is not valid JSON throws `RequestJsonException`, answered `400`.
- `GetBodyStream()` gives the body as a stream that can be read once. `BufferBodyAsync()` reads
  it into memory, after which it can be read any number of times.
- A body that is too large or too slow fails the read with `413` or `408` and closes the
  connection. A body the handler did not read is drained before the next request.

---

## Building a response

`context.Response()` returns a builder. The status and headers come first, then exactly one
method that sets the body and returns the `HttpResponse`:

```csharp
return context.Response().Json(note);
return context.Response().Status(StatusCodes.Created).Json(note);
return context.Response().Status(StatusCodes.NoContent).Empty();
return context.Response().AddHeader("Cache-Control", "no-store").Text("ok");
return context.Response().Bytes(html, "text/html; charset=utf-8");
return context.Response().Stream(async (stream, ct) => await file.CopyToAsync(stream, ct), "application/pdf");
```

The status defaults to `200`. `Content-Length`, `Transfer-Encoding` and `Keep-Alive` are set by
the server and cannot be added; `Connection` accepts only `close`, which closes the connection
after the response. A stream without a length is sent chunked to HTTP/1.1 clients.

---

## Middleware and filters

Middleware and filters implement the same interface:

```csharp
public class LoggingMiddleware : IMiddleware
{
    public async Task<HttpResponse> InvokeAsync(HttpRequest request, HttpContext context, RequestDelegate next)
    {
        var response = await next(request, context);
        Console.WriteLine($"{request.Method} {request.Path} -> {response.StatusCode}");
        return response;
    }
}
```

`RegisterMiddleware<T>()` runs it for every request, before routing, in registration order.
`AddFilter<T>()` on a group or an endpoint runs it only for that part of the route tree.
A middleware or filter that returns a response without calling `next` ends the request there:

```csharp
if (request.GetFromHeaders("X-Api-Key") != _apiKey)
    return Task.FromResult(context.Response().Status(StatusCodes.Unauthorized).Text("Missing X-Api-Key"));
return next(request, context);
```

---

## Errors

An exception thrown by a handler passes back through the filters and middleware, so they can
catch it, log it or turn it into another response. An exception nobody caught is answered by
the framework:

| Exception                                         | Response | Connection |
|---------------------------------------------------|----------|------------|
| `RequestValueException`, `RequestJsonException`   | `400`    | kept       |
| body too large or malformed                       | `413`, `400` | closed |
| body too slow                                     | `408`    | closed     |
| anything else                                     | `500`, logged | kept  |

Errors in the request head (`400`, `408`, `431`, and `413` for a declared `Content-Length`
over the limit) are answered before the pipeline, so middleware does not see them.

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

| Key                                 | Default   | Meaning                                                                  |
|-------------------------------------|-----------|--------------------------------------------------------------------------|
| `Server:Port`                       | `5000`    | listening port; `0` lets the OS choose                                   |
| `Server:ListenBacklog`              | `512`     | OS queue of connections not yet accepted; the OS may cap it              |
| `Server:AcceptLoops`                | `1`       | parallel accept loops, at least `1`                                      |
| `Server:MaxConnections`             | `10000`   | open connections; above it a new connection gets `503`                   |
| `Server:MaxConcurrentRequests`      | `256`     | requests being handled at once                                           |
| `Server:MaxQueuedRequests`          | `1024`    | requests waiting for a slot; full ⇒ `503`                                |
| `Server:RequestQueueTimeoutSeconds` | `2`       | how long a request waits for a slot ⇒ `503`; handling itself is not timed |
| `Server:MaxHeadBytes`               | `32768`   | request head limit ⇒ `431`                                               |
| `Server:MaxBodyBytes`               | `1048576` | request body limit ⇒ `413`                                               |
| `Server:HeadersTimeoutSeconds`      | `10`      | time to send the head once its first byte arrived ⇒ `408`                |
| `Server:BodyGracePeriodSeconds`     | `5`       | time a body may take before the minimum rate applies                     |
| `Server:MinBodyBytesPerSecond`      | `240`     | minimum body upload rate ⇒ `408`                                         |
| `Server:KeepAliveMax`               | `1000`    | requests served on one connection before it is closed                    |
| `Server:KeepAliveTimeoutSeconds`    | `60`      | seconds a connection may sit idle before the next request                |

`KeepAliveTimeoutSeconds` limits the pause *between* requests, not the life of a connection.
An idle connection costs only memory, so the timeout can be long. Behind a reverse proxy that
keeps an upstream pool it has to stay above the proxy's own idle timeout — otherwise the server
closes a pooled connection under the proxy and the client sees `502`.

The body limit matters for memory: `ReadJsonAsync` and `BufferBodyAsync` hold the whole body,
so the worst case is `MaxBodyBytes × MaxConcurrentRequests`. Raise it for an endpoint that
reads the body as a stream through `GetBodyStream()`, not for JSON.

---

## License

Apache License 2.0 — see [LICENSE](LICENSE).
