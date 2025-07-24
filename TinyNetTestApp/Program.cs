using TinyNet.Application;

var builder = new AppBuilder();
builder
    .AddJsonConfig("config.json");

var app = builder.Build();
await app.Run();