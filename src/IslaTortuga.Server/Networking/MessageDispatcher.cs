using System.Text.Json;
using IslaTortuga.Server.Networking.Protocol;
using IslaTortuga.Server.Networking.Protocol.Payloads;
using IslaTortuga.Server.Replication;
using IslaTortuga.Server.Rooms;
using IslaTortuga.Server.Sessions;

namespace IslaTortuga.Server.Networking;

public sealed class MessageDispatcher
{
    private readonly GameTicketService _gameTicketService;
    private readonly SessionManager _sessionManager;
    private readonly GameRoomManager _gameRoomManager;
    private readonly SnapshotBuilder _snapshotBuilder;

    public MessageDispatcher(
        GameTicketService gameTicketService,
        SessionManager sessionManager,
        GameRoomManager gameRoomManager,
        SnapshotBuilder snapshotBuilder)
    {
        _gameTicketService = gameTicketService;
        _sessionManager = sessionManager;
        _gameRoomManager = gameRoomManager;
        _snapshotBuilder = snapshotBuilder;
    }

    public async Task DispatchAsync(
        ClientConnection connection,
        NetworkEnvelope envelope,
        CancellationToken cancellationToken)
    {
        switch (envelope.Op)
        {
            case ProtocolTypes.AuthJoin:
                await HandleJoinAsync(connection, envelope, cancellationToken);
                break;

            case ProtocolTypes.AuthReconnect:
                await HandleReconnectAsync(connection, envelope, cancellationToken);
                break;

            case ProtocolTypes.PlayerInput:
                await HandlePlayerInputAsync(connection, envelope, cancellationToken);
                break;

            case ProtocolTypes.Ping:
                await connection.SendAsync(ProtocolTypes.Pong, new { }, envelope.RequestId, cancellationToken);
                break;

            default:
                await connection.SendAsync(
                    ProtocolTypes.Error,
                    new ErrorPayload("unknown_op", $"Unsupported operation '{envelope.Op}'."),
                    envelope.RequestId,
                    cancellationToken);
                break;
        }
    }

    private async Task HandleJoinAsync(
        ClientConnection connection,
        NetworkEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var payload = DeserializePayload<JoinGamePayload>(envelope);
        if (payload is null)
        {
            await SendInvalidPayloadAsync(connection, envelope, cancellationToken);
            return;
        }

        if (!_gameTicketService.TryConsume(payload.GameTicket, TicketPurpose.Join, out var ticket, out var errorCode))
        {
            await connection.SendAsync(
                ProtocolTypes.AuthRejected,
                new ErrorPayload(errorCode, "The game ticket is invalid or expired.", true),
                envelope.RequestId,
                cancellationToken);
            return;
        }

        var session = _sessionManager.CreateSession(ticket!, connection.ConnectionId);
        connection.BindSession(session);

        var roomPlayer = _gameRoomManager.AttachOrGetSession(session);

        await connection.SendAsync(
            ProtocolTypes.AuthAccepted,
            new AuthAcceptedPayload(
                session.SessionId,
                session.UserId,
                session.DisplayName,
                roomPlayer.Room.RoomId,
                roomPlayer.PlayerEntity.EntityId),
            envelope.RequestId,
            cancellationToken);

        await connection.SendAsync(
            ProtocolTypes.WorldSnapshot,
            _snapshotBuilder.Build(roomPlayer.Room, roomPlayer),
            cancellationToken: cancellationToken);
    }

    private async Task HandleReconnectAsync(
        ClientConnection connection,
        NetworkEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var payload = DeserializePayload<ReconnectPayload>(envelope);
        if (payload is null)
        {
            await SendInvalidPayloadAsync(connection, envelope, cancellationToken);
            return;
        }

        if (!_gameTicketService.TryConsume(payload.GameTicket, TicketPurpose.Reconnect, out var ticket, out var errorCode))
        {
            await connection.SendAsync(
                ProtocolTypes.AuthRejected,
                new ErrorPayload(errorCode, "The reconnect ticket is invalid or expired.", true),
                envelope.RequestId,
                cancellationToken);
            return;
        }

        var session = _sessionManager.ReconnectSession(ticket!, connection.ConnectionId);
        connection.BindSession(session);

        var roomPlayer = _gameRoomManager.AttachOrGetSession(session);

        await connection.SendAsync(
            ProtocolTypes.AuthAccepted,
            new AuthAcceptedPayload(
                session.SessionId,
                session.UserId,
                session.DisplayName,
                roomPlayer.Room.RoomId,
                roomPlayer.PlayerEntity.EntityId),
            envelope.RequestId,
            cancellationToken);

        await connection.SendAsync(
            ProtocolTypes.WorldSnapshot,
            _snapshotBuilder.Build(roomPlayer.Room, roomPlayer),
            cancellationToken: cancellationToken);
    }

    private async Task HandlePlayerInputAsync(
        ClientConnection connection,
        NetworkEnvelope envelope,
        CancellationToken cancellationToken)
    {
        if (connection.Session is null)
        {
            await connection.SendAsync(
                ProtocolTypes.Error,
                new ErrorPayload("not_authenticated", "Join the game before sending input."),
                envelope.RequestId,
                cancellationToken);
            return;
        }

        var payload = DeserializePayload<PlayerInputPayload>(envelope);
        if (payload is null)
        {
            await SendInvalidPayloadAsync(connection, envelope, cancellationToken);
            return;
        }

        var roomPlayer = _gameRoomManager.AttachOrGetSession(connection.Session);
        roomPlayer.PlayerEntity.ApplyInput(payload.MoveX, payload.MoveY);
    }

    private static TPayload? DeserializePayload<TPayload>(NetworkEnvelope envelope)
    {
        if (envelope.Payload is null)
        {
            return default;
        }

        return envelope.Payload.Value.Deserialize<TPayload>(ProtocolJson.SerializerOptions);
    }

    private static Task SendInvalidPayloadAsync(
        ClientConnection connection,
        NetworkEnvelope envelope,
        CancellationToken cancellationToken)
    {
        return connection.SendAsync(
            ProtocolTypes.Error,
            new ErrorPayload("invalid_payload", $"Invalid payload for operation '{envelope.Op}'."),
            envelope.RequestId,
            cancellationToken);
    }
}
