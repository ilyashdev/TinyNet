namespace TinyNet.Http;

public delegate Task<HttpResponse> RequestDelegate(HttpRequest request, HttpContext context);