using TinyNet.Http;

namespace TinyNet.Middleware;

public delegate Task RequestDelegate(HttpContext context);