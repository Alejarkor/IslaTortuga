using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace IslaTortuga.Server.Sessions;

public sealed class GameTicketService
{
    private static readonly TimeSpan TicketLifetime = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions TicketJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ConcurrentDictionary<string, byte> _consumedTicketIds = new();

    public GameTicket CreateJoinTicket(SessionCookiePrincipal principal)
    {
        return CreateSignedTicket(
            principal.UserId,
            principal.DisplayName,
            TicketPurpose.Join,
            previousSessionId: null);
    }

    public GameTicket CreateReconnectTicket(SessionCookiePrincipal principal, string? previousSessionId)
    {
        return CreateSignedTicket(
            principal.UserId,
            principal.DisplayName,
            TicketPurpose.Reconnect,
            previousSessionId);
    }

    public bool TryConsume(
        string signedTicket,
        TicketPurpose expectedPurpose,
        out GameTicket? ticket,
        out string errorCode)
    {
        ticket = null;
        errorCode = "ticket_invalid";

        var payload = ValidateSignedTicket(signedTicket);
        if (payload is null)
        {
            return false;
        }

        if (payload.ExpiresAt <= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        {
            errorCode = "ticket_expired";
            return false;
        }

        if (!string.Equals(payload.Purpose, ToPurposeValue(expectedPurpose), StringComparison.OrdinalIgnoreCase))
        {
            errorCode = "ticket_purpose_mismatch";
            return false;
        }

        if (!_consumedTicketIds.TryAdd(payload.TicketId, 0))
        {
            errorCode = "ticket_already_used";
            return false;
        }

        ticket = new GameTicket(
            payload.TicketId,
            payload.UserId,
            payload.DisplayName,
            expectedPurpose,
            payload.PreviousSessionId,
            DateTimeOffset.FromUnixTimeMilliseconds(payload.ExpiresAt));
        errorCode = string.Empty;
        return true;
    }

    private GameTicket CreateSignedTicket(
        string userId,
        string displayName,
        TicketPurpose purpose,
        string? previousSessionId)
    {
        var payload = new SignedGameTicketPayload(
            Guid.NewGuid().ToString("N"),
            userId,
            displayName,
            ToPurposeValue(purpose),
            previousSessionId,
            DateTimeOffset.UtcNow.Add(TicketLifetime).ToUnixTimeMilliseconds());

        var serializedPayload = JsonSerializer.Serialize(payload);
        var encodedPayload = Base64UrlEncode(Encoding.UTF8.GetBytes(serializedPayload));
        var signature = ComputeSignature(encodedPayload);
        var signedTicket = $"{encodedPayload}.{signature}";

        return new GameTicket(
            signedTicket,
            userId,
            displayName,
            purpose,
            previousSessionId,
            DateTimeOffset.FromUnixTimeMilliseconds(payload.ExpiresAt));
    }

    private SignedGameTicketPayload? ValidateSignedTicket(string signedTicket)
    {
        var parts = signedTicket.Split('.', 2);
        if (parts.Length != 2)
        {
            return null;
        }

        var encodedPayload = parts[0];
        var expectedSignature = ComputeSignature(encodedPayload);

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(parts[1]),
                Encoding.UTF8.GetBytes(expectedSignature)))
        {
            return null;
        }

        var payloadBytes = Base64UrlDecode(encodedPayload);
        return JsonSerializer.Deserialize<SignedGameTicketPayload>(payloadBytes, TicketJsonOptions);
    }

    private string ComputeSignature(string encodedPayload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(GetTicketSecret()));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(encodedPayload));
        return Base64UrlEncode(hash);
    }

    private static string ToPurposeValue(TicketPurpose purpose)
    {
        return purpose == TicketPurpose.Reconnect ? "reconnect" : "join";
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value
            .Replace('-', '+')
            .Replace('_', '/');

        padded = padded.PadRight(padded.Length + ((4 - padded.Length % 4) % 4), '=');
        return Convert.FromBase64String(padded);
    }

    private static string GetTicketSecret()
    {
        return Environment.GetEnvironmentVariable("GAME_TICKET_SECRET")
            ?? "dev_game_ticket_secret_change_me";
    }

    private sealed record SignedGameTicketPayload(
        string TicketId,
        string UserId,
        string DisplayName,
        string Purpose,
        string? PreviousSessionId,
        long ExpiresAt);
}
