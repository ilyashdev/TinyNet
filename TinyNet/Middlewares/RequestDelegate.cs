using TinyNet.Http;

namespace TinyNet.Middlewares;

public delegate Task RequestDelegate(HttpContext context);