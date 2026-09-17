using TinyNet.Application;
using TinyNetTestApp;

var builder = new AppBuilder();
builder
    .AddJsonConfig("config.json")
    .AddEnvironmentVariables("TINYNET_");

builder.Services.AddSingleton<SingletonService>();

var app = builder.Build();
await app.Run();