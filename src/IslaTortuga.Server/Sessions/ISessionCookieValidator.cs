namespace IslaTortuga.Server.Sessions;

public interface ISessionCookieValidator
{
    Task<SessionCookiePrincipal?> ValidateAsync(HttpContext httpContext, CancellationToken cancellationToken);
}
