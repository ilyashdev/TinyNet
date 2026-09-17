using TinyNet.Application;
using TinyNetTestApp;

var builder = new AppBuilder();
builder
    .AddJsonConfig("config.json")
    .AddEnvironmentVariables("TINYNET_");

var app = builder.Build();
await app.Run();