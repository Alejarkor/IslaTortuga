using Microsoft.Extensions.FileProviders;
using IslaTortuga.Server.Api;
using IslaTortuga.Server.Content;
using IslaTortuga.Server.GameLoop;
using IslaTortuga.Server.Networking;
using IslaTortuga.Server.Replication;
using IslaTortuga.Server.Rooms;
using IslaTortuga.Server.Sessions;
using IslaTortuga.Server.World.Tiled;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://0.0.0.0:{ResolveGameServerPort()}");

builder.Services.AddRouting();

builder.Services.AddSingleton<InMemorySessionCookieStore>();
builder.Services.AddSingleton<ISessionCookieValidator, CookieSessionValidator>();
builder.Services.AddSingleton<GameTicketService>();
builder.Services.AddSingleton<SessionManager>();
builder.Services.AddSingleton<ContentIndexLoader>();
builder.Services.AddSingleton<ConnectionManager>();
builder.Services.AddSingleton<MessageDispatcher>();
builder.Services.AddSingleton<WebSocketGateway>();
builder.Services.AddSingleton<TiledWorldBuilder>();
builder.Services.AddSingleton<GameRoomManager>();
builder.Services.AddSingleton<InterestManager>();
builder.Services.AddSingleton<EntityReplicator>();
builder.Services.AddSingleton<SnapshotBuilder>();
builder.Services.AddHostedService<GameTickService>();

var app = builder.Build();
var contentRootPath = ContentPathResolver.ResolveContentRoot(app.Environment.ContentRootPath);

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(15),
});

if (Directory.Exists(contentRootPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(contentRootPath),
        RequestPath = "/content",
    });
}

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    server = "IslaTortuga.Server",
    utcNow = DateTimeOffset.UtcNow,
}));

var api = app.MapGroup("/api");
AuthEndpoints.Map(api);
DevGameEndpoints.Map(api);

app.Map("/ws/game", (HttpContext context, WebSocketGateway gateway, CancellationToken cancellationToken) =>
    gateway.AcceptAsync(context, cancellationToken));

await app.RunAsync();

static int ResolveGameServerPort()
{
    var rawValue = Environment.GetEnvironmentVariable("GAME_SERVER_PORT");
    return int.TryParse(rawValue, out var port) ? port : 5055;
}
