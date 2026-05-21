using IslaTortuga.Server.Networking.Protocol.Payloads;
using IslaTortuga.Server.Sessions;

namespace IslaTortuga.Server.Networking.Protocol;

/// <summary>
/// Fábrica estática para construir los payloads del protocolo de red.
/// No serializa — la serialización ocurre en ClientConnection.SendAsync.
/// </summary>
public static class PayloadBuilder
{
    public static AuthAcceptedPayload AuthAccepted(
        PlayerSession session,
        string roomId,
        string playerEntityId) =>
        new(
            session.SessionId,
            session.UserId,
            session.DisplayName,
            roomId,
            playerEntityId);

    /// <summary>
    /// Error recuperable: el cliente puede reintentar (ticket expirado, etc.).
    /// </summary>
    public static ErrorPayload AuthRejected(string code, string message) =>
        new(code, message, Retryable: true);

    /// <summary>
    /// Error general no recuperable (payload inválido, op desconocida, etc.).
    /// </summary>
    public static ErrorPayload Error(string code, string message) =>
        new(code, message, Retryable: false);

    public static object Pong() => new { };
}
