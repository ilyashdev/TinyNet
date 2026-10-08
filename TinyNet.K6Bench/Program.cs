using TinyNet.Application;
using TinyNet.K6Bench;

var builder = new AppBuilder();
builder
    .AddJsonConfig("config.json")
    .AddEnvironmentVariables("TINYNET_");

builder.Routes.AddGet("/", LoadHandlers.Ping);

builder.Routes
    .AddGroup("/load")
    .AddGet("/cpu", LoadHandlers.Cpu)
    .AddGet("/io", LoadHandlers.Io)
    .AddGet("/block", LoadHandlers.Block);

var app = builder.Build();
await app.Run();
