using System.Collections.Concurrent;

namespace IslaTortuga.Server.Sessions;

public sealed class InMemorySessionCookieStore
{
    private readonly ConcurrentDictionary<string, SessionCookiePrincipal> _cookies = new();

    public SessionCookiePrincipal Issue(string userId, string displayName)
    {
        var principal = new SessionCookiePrincipal(
            Guid.NewGuid().ToString("N"),
            userId,
            displayName);

        _cookies[principal.SessionCookieId] = principal;
        return principal;
    }

    public bool TryGet(string sessionCookieId, out SessionCookiePrincipal? principal)
    {
        return _cookies.TryGetValue(sessionCookieId, out principal);
    }

    public bool Remove(string sessionCookieId)
    {
        return _cookies.TryRemove(sessionCookieId, out _);
    }
}
