namespace IslaTortuga.Server.Sessions;

public sealed record GameTicket(
    string TicketId,
    string UserId,
    string DisplayName,
    TicketPurpose Purpose,
    string? PreviousSessionId,
    DateTimeOffset ExpiresAt);
