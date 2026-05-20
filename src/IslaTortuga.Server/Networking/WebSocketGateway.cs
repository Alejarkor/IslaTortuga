using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using IslaTortuga.Server.Networking.Protocol;
using IslaTortuga.Server.Networking.Protocol.Payloads;
using IslaTortuga.Server.Sessions;

namespace IslaTortuga.Server.Networking;

public sealed class WebSocketGateway
{
    private readonly ConnectionManager _connectionManager;
    private readonly MessageDispatcher _messageDispatcher;
    private readonly SessionManager _sessionManager;

    public WebSocketGateway(
        ConnectionManager connectionManager,
        MessageDispatcher messageDispatcher,
        SessionManager sessionManager)
    {
        _connectionManager = connectionManager;
        _messageDispatcher = messageDispatcher;
        _sessionManager = sessionManager;
    }

    public async Task AcceptAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (!httpContext.WebSockets.IsWebSocketRequest)
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var socket = await httpContext.WebSockets.AcceptWebSocketAsync();
        var connection = _connectionManager.Add(socket);

        try
        {
            await ReceiveLoopAsync(connection, cancellationToken);
        }
        finally
        {
            _sessionManager.MarkDisconnected(connection.ConnectionId);
            _connectionManager.Remove(connection.ConnectionId, out _);

            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "closed",
                    CancellationToken.None);
            }
        }
    }

    private async Task ReceiveLoopAsync(ClientConnection connection, CancellationToken cancellationToken)
    {
        var buffer = new byte[8 * 1024];

        while (!cancellationToken.IsCancellationRequested && connection.Socket.State == WebSocketState.Open)
        {
            using var messageStream = new MemoryStream();
            WebSocketReceiveResult receiveResult;

            do
            {
                receiveResult = await connection.Socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    cancellationToken);

                if (receiveResult.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }

                await messageStream.WriteAsync(buffer.AsMemory(0, receiveResult.Count), cancellationToken);
            }
            while (!receiveResult.EndOfMessage);

            var message = Encoding.UTF8.GetString(messageStream.ToArray());
            NetworkEnvelope? envelope;

            try
            {
                envelope = JsonSerializer.Deserialize<NetworkEnvelope>(message, ProtocolJson.SerializerOptions);
            }
            catch (JsonException)
            {
                await connection.SendAsync(
                    ProtocolTypes.Error,
                    new ErrorPayload("invalid_json", "The message is not valid JSON."),
                    cancellationToken: cancellationToken);
                continue;
            }

            if (envelope is null || string.IsNullOrWhiteSpace(envelope.Op))
            {
                await connection.SendAsync(
                    ProtocolTypes.Error,
                    new ErrorPayload("invalid_envelope", "The network envelope is incomplete."),
                    cancellationToken: cancellationToken);
                continue;
            }

            await _messageDispatcher.DispatchAsync(connection, envelope, cancellationToken);
        }
    }
}
