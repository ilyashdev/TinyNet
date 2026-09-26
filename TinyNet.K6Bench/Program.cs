using TinyNet.Application;
using TinyNet.K6Bench;

var builder = new AppBuilder();
builder
    .AddJsonConfig("config.json")
    .AddEnvironmentVariables("TINYNET_");

builder.Routes
    .AddGetHandler<PingController>();

var load = builder.Routes.AddGroup("/load");
load.AddGroup("/cpu").AddGetHandler<CpuLoadController>();
load.AddGroup("/io").AddGetHandler<IoLoadController>();
load.AddGroup("/block").AddGetHandler<BlockLoadController>();

var app = builder.Build();
await app.Run();