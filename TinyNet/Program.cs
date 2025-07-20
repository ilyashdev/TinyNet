using TinyNet.Application;
using TinyNet.Test.Middleware;

var builder = new AppBuilder();

builder.Pipeline.RegisterMiddleware<TestMiddleware>();

var app = builder.Build();
await app.Run();
