using CCWebSockets;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<WebSocketMiddleware>();
builder.Host.UseSerilog((ctx, logger) =>
{
    logger
        .MinimumLevel.Information()
        .WriteTo.Console();
});

builder.WebHost.UseUrls("http://0.0.0.0:30303");

var app = builder.Build();

app.UseWebSockets();
app.UseMiddleware<WebSocketMiddleware>();

app.MapGet("/w.lua", (HttpContext ctx) =>
{
    return File
        .ReadAllText("w.lua")
        .Replace("<url>", ctx.Request.Host.Value);
});

app.Run();
