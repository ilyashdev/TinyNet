# TinyNet

A lightweight, ASP.NET Core-inspired HTTP web framework built from scratch in C# .NET 9.

---

## Table of Contents

- [Overview](#overview)
- [Getting Started](#getting-started)
- [Architecture](#architecture)
- [Modules](#modules)
  - [Application](#application)
  - [Dependency Injection](#dependency-injection)
  - [Controllers & Routing](#controllers--routing)
  - [Middleware & Filters](#middleware--filters)
  - [Configuration](#configuration)
  - [HTTP](#http)
  - [Action Results](#action-results)
  - [Static Files](#static-files)
- [Examples](#examples)
- [License](#license)

---

## Overview

TinyNet is a minimal HTTP server framework that implements the core concepts of modern web frameworks:

- Fluent application builder
- Dependency injection with Singleton, Scoped, and Transient lifetimes
- Middleware pipeline with per-controller filter support
- Attribute-based routing and HTTP method mapping
- Multi-provider configuration system
- Async request processing via `Channel<T>` + worker pool
- Static file serving

---

## Getting Started

### 1. Create the application

```csharp
var builder = new AppBuilder();

builder.AddJsonConfig("config.json");
builder.AddEnvironmentVariables("MYAPP_");

builder.Services.AddSingleton<MyService>();
builder.RegisterMiddleware<LoggingMiddleware>();

var app = builder.Build();
await app.Run();
```

### 2. Create a controller

```csharp
[Route("/hello")]
public class HelloController : Controller
{
    private readonly MyService _service;

    public HelloController(MyService service)
    {
        _service = service;
    }

    [HttpMethod("GET")]
    public IActionResult Get()
    {
        return new Ok(new { message = "Hello, World!" });
    }

    [HttpMethod("POST")]
    public IActionResult Post([FromBody] string name)
    {
        return new Ok(new { message = $"Hello, {name}!" });
    }
}
```

### 3. Configuration file (`config.json`)

```json
{
  "Server": {
    "Port": 5000
  },
  "WebRoot": {
    "Path": "/wwwroot"
  }
}
```

---

## Architecture

```
AppBuilder
    │
    ├── DIContainer         — service registration & resolution
    ├── ConfigurationBuilder — configuration providers
    └── MiddlewarePipeline  — middleware registration
            │
            ▼
    WebApplication
            │
            ├── NetHandler (AcceptAsync)
            │       │
            │       ▼
            ├── Channel<NetClient>   — request buffer
            │       │
            │       ▼
            └── Worker Pool (N = ProcessorCount * 2 - 1)
                    │
                    ▼
            ProcessClient
                    ├── DIScope (per-request)
                    ├── HttpRequest parsing
                    ├── MiddlewarePipeline.InvokeAsync
                    │       └── ControllerHandler.Handle
                    │               ├── Controller resolution (DI)
                    │               ├── Parameter binding
                    │               └── Method invocation
                    └── HttpResponse sending
```

### Request flow

1. `NetHandler.AcceptAsync()` accepts a TCP connection
2. `NetClient` is written to an unbounded `Channel<NetClient>`
3. One of N workers reads the client from the channel
4. A `DIScope` is created for the request lifetime
5. `HttpRequest` is parsed from raw TCP data
6. `MiddlewarePipeline` builds and invokes the middleware chain
7. `ControllerHandler` resolves the controller via DI, binds parameters, invokes the method
8. `IActionResult.ExecuteResult` writes the `HttpResponse`
9. The response is sent over the socket; the socket is closed

---

## Modules

### Application

#### `AppBuilder`

The entry point for configuring and building the application.

```csharp
var builder = new AppBuilder();
```

| Method | Description |
|--------|-------------|
| `AddJsonConfig(string path)` | Adds a JSON configuration file |
| `AddEnvironmentVariables(string? prefix)` | Adds environment variables as a configuration source |
| `RegisterMiddleware<T>()` | Registers a global middleware |
| `RegisterFilter<T>()` | Registers a conditional per-controller filter middleware |
| `Services` | Exposes the `DIContainer` for service registration |
| `Build()` | Builds and returns the `WebApplication` |

#### `WebApplication`

```csharp
await app.Run();
```

Starts the server. Internally:
- Spawns an async accept loop writing clients to a `Channel<NetClient>`
- Starts N worker tasks (`ProcessorCount * 2 - 1`) reading from the channel via `ReadAllAsync`

---

### Dependency Injection

`DIContainer` supports three service lifetimes:

| Lifetime | Behavior |
|----------|----------|
| `Singleton` | One instance for the entire application lifetime |
| `Scoped` | One instance per HTTP request (`DIScope`) |
| `Transient` | New instance every time it is requested |

#### Registration

```csharp
// By interface and implementation
builder.Services.AddSingleton<IMyService, MyService>();
builder.Services.AddScoped<IMyService, MyService>();
builder.Services.AddTransient<IMyService, MyService>();

// By implementation type only
builder.Services.AddSingleton<MyService>();

// By Type object (used internally)
builder.Services.AddTransient(typeof(MyService));

// Pre-built instance
builder.Services.AddInstance<IMyService>(existingInstance);
```

#### Constructor injection

Controllers and middleware receive their dependencies via constructor injection automatically.

```csharp
[Route("/users")]
public class UsersController : Controller
{
    public UsersController(IUserRepository repo, ILogger logger) { ... }
}
```

#### Cyclic dependency detection

The container detects cyclic dependencies at resolution time and throws `InvalidOperationException`.

#### Performance

Constructor invocation is compiled to a native delegate via `Expression.Lambda` on first use and cached — subsequent resolutions do not use reflection.

---

### Controllers & Routing

#### Defining a controller

A controller must:
- Inherit from `Controller`
- Be decorated with `[Route]`
- Have at least one method decorated with `[HttpMethod]`

```csharp
[Route("/products")]
public class ProductsController : Controller
{
    [HttpMethod("GET")]
    public IActionResult GetAll()
    {
        return new Ok(new[] { "product1", "product2" });
    }

    [HttpMethod("POST")]
    public IActionResult Create([FromBody] string name)
    {
        return new Ok(new { created = name });
    }
}
```

#### Parameter binding

| Attribute | Source | Type |
|-----------|--------|------|
| `[FromBody]` | JSON request body | Any JSON-deserializable type |
| `[FromQuery]` | URL query string | `string` |

```csharp
[HttpMethod("GET")]
public IActionResult Search([FromQuery] string term)
{
    return new Ok(new { query = term });
}

[HttpMethod("POST")]
public IActionResult Submit([FromBody] string payload)
{
    return new Ok(payload);
}
```

#### Excluding a controller from routing

```csharp
[NotMapped]
public class InternalController : Controller { ... }
```

#### Supported HTTP methods

`GET`, `POST`, `PUT`, `DELETE`, `PATCH`, `HEAD`, `OPTIONS`

---

### Middleware & Filters

#### Creating middleware

Inherit from `Middleware` and implement `InvokeAsync`:

```csharp
public class LoggingMiddleware : Middleware
{
    public LoggingMiddleware(RequestDelegate next) : base(next) { }

    public override async Task InvokeAsync(HttpContext context)
    {
        Console.WriteLine($"→ {context.Request.Method} {context.Request.Url}");
        await _next(context);
        Console.WriteLine($"← {context.Response?.StatusCode}");
    }
}
```

#### Registering global middleware

Runs for every request:

```csharp
builder.RegisterMiddleware<LoggingMiddleware>();
```

#### Registering filter middleware

Runs only for controllers decorated with `[Filter(typeof(T))]`:

```csharp
builder.RegisterFilter<AuthMiddleware>();
```

```csharp
[Route("/admin")]
[Filter(typeof(AuthMiddleware))]
public class AdminController : Controller { ... }
```

#### Middleware execution order

Global middlewares run first, then filter middlewares applicable to the matched controller. All execute in registration order.

---

### Configuration

#### Providers

| Provider | Registration |
|----------|-------------|
| JSON file | `builder.AddJsonConfig("config.json")` |
| Environment variables | `builder.AddEnvironmentVariables("PREFIX_")` |

Providers added later take priority over earlier ones.

#### Accessing configuration

Configuration is available via `IConfiguration` in any DI-resolved class:

```csharp
public class MyService
{
    public MyService(IConfiguration config)
    {
        var port = config["Server:Port"];
        var section = config.GetSection("Server");
        var portFromSection = section["Port"];
    }
}
```

#### Key format

Nested JSON keys are flattened with `:` as separator:

```json
{ "Server": { "Port": 5000 } }
```
→ `config["Server:Port"]` = `"5000"`

Environment variables use `__` as separator and it is converted to `:`:

```
PREFIX_Server__Port=5000
```
→ `config["Server:Port"]` = `"5000"`

---

### HTTP

#### `HttpRequest`

| Property | Type | Description |
|----------|------|-------------|
| `Method` | `string` | HTTP method (`GET`, `POST`, etc.) |
| `Url` | `string` | Request path |
| `Headers` | `Dictionary<string, string>` | Request headers (case-insensitive) |
| `Query` | `Dictionary<string, string>` | Parsed query string parameters |
| `Body` | `JsonObject?` | Parsed JSON body |

#### `HttpResponse`

| Property | Type | Description |
|----------|------|-------------|
| `StatusCode` | `int?` | HTTP status code |
| `Headers` | `Dictionary<string, string>` | Response headers |
| `Body` | `string?` | Text response body |
| `BinaryBody` | `byte[]?` | Binary response body |

Responses are serialized via `ToHttpResponse()` (text) or `ToHttpResponseBytes()` (binary). `Content-Length` is set automatically.

---

### Action Results

Action results encapsulate the HTTP response. All implement `IActionResult`.

| Class | Status | Description |
|-------|--------|-------------|
| `Ok` | 200 | Success, optional JSON body |
| `BadRequest` | 400 | Client error, optional JSON body |
| `NotFound` | 404 | Resource not found |
| `InternalError` | 500 | Server error |
| `HtmlView` | 200 | HTML response |
| `Media` | 200 | Text or binary content with custom Content-Type |

#### Usage

```csharp
return new Ok();                          // 200 empty
return new Ok(new { id = 1 });           // 200 with JSON body
return new BadRequest("Invalid input");  // 400 with message
return new NotFound();                   // 404
return new HtmlView("<h1>Hello</h1>");   // 200 text/html
return new Media(bytes, "image/png");    // 200 binary
return new Media(text, "text/csv");      // 200 text
```

#### Custom action result

```csharp
public class Created : BaseResult
{
    public Created(object data) : base(201, data) { }
}
```

---

### Static Files

Static files are served automatically for any URL containing a `.` (e.g. `/style.css`, `/logo.png`).

Configure the web root in `config.json`:

```json
{
  "WebRoot": {
    "Path": "/wwwroot"
  }
}
```

Files are resolved relative to the application's working directory. If the file does not exist, a `404 Not Found` response is returned.

#### Supported content types

`html`, `css`, `js`, `json`, `xml`, `jpeg`, `jpg`, `png`, `bmp`, `gif`, `tiff`, `webp`, `zip`, `rar`

Any other extension is served as `application/octet-stream`.

---

## Examples

### Full controller example

```csharp
[Route("/api/items")]
public class ItemsController : Controller
{
    private readonly ItemService _service;

    public ItemsController(ItemService service)
    {
        _service = service;
    }

    [HttpMethod("GET")]
    public IActionResult GetAll()
    {
        var items = _service.GetAll();
        return new Ok(items);
    }

    [HttpMethod("POST")]
    public async Task<IActionResult> Create([FromBody] string name)
    {
        var item = await _service.CreateAsync(name);
        return new Ok(item);
    }
}
```

### Middleware with DI

```csharp
public class AuthMiddleware : Middleware
{
    private readonly IConfiguration _config;

    public AuthMiddleware(RequestDelegate next, IConfiguration config) : base(next)
    {
        _config = config;
    }

    public override async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("Authorization", out var token))
        {
            context.Response = new HttpResponse(401, "Unauthorized");
            return;
        }
        await _next(context);
    }
}
```

### Custom action result

```csharp
public class NoContent : ActionResult
{
    public NoContent() : base(204) { }
}
```

---

## License

TinyNet is licensed under the **Apache License 2.0**.

You are free to use, modify, and distribute this software in personal and commercial projects. Any modifications must retain the original copyright notice. The license also provides explicit protection against patent claims.

See the [LICENSE](LICENSE) file for the full license text.
