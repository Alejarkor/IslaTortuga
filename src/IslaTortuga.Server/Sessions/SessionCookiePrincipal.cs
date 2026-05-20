namespace IslaTortuga.Server.Sessions;

public sealed record SessionCookiePrincipal(
    string SessionCookieId,
    string UserId,
    string DisplayName);
