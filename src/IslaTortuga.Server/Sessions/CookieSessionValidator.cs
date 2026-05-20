namespace IslaTortuga.Server.Sessions;

public sealed class CookieSessionValidator : ISessionCookieValidator
{
    public const string CookieName = "isla_tortuga_session";

    private readonly InMemorySessionCookieStore _cookieStore;

    public CookieSessionValidator(InMemorySessionCookieStore cookieStore)
    {
        _cookieStore = cookieStore;
    }

    public Task<SessionCookiePrincipal?> ValidateAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!httpContext.Request.Cookies.TryGetValue(CookieName, out var cookieValue))
        {
            return Task.FromResult<SessionCookiePrincipal?>(null);
        }

        return Task.FromResult(
            _cookieStore.TryGet(cookieValue, out var principal)
                ? principal
                : null);
    }
}
