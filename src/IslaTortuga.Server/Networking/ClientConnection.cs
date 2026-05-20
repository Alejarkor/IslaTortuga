using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using IslaTortuga.Server.Networking.Protocol;
using IslaTortuga.Server.Sessions;

namespace IslaTortuga.Server.Networking;

public sealed class ClientConnection
{
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public ClientConnection(WebSocket socket)
    {
        Socket = socket;
    }

    public string ConnectionId { get; } = Guid.NewGuid().ToString("N");

    public WebSocket Socket { get; }

    public PlayerSession? Session { get; private set; }

    public void BindSession(PlayerSession session)
    {
        Session = session;
    }

    public async Task SendAsync<TPayload>(
        string op,
        TPayload payload,
        string? requestId = null,
        CancellationToken cancellationToken = default)
    {
        var envelope = new
        {
            op,
            requestId,
            sentAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            payload,
        };

        var json = JsonSerializer.Serialize(envelope, ProtocolJson.SerializerOptions);
        var buffer = Encoding.UTF8.GetBytes(json);

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            if (Socket.State != WebSocketState.Open)
            {
                return;
            }

            await Socket.SendAsync(
                new ArraySegment<byte>(buffer),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }
}
