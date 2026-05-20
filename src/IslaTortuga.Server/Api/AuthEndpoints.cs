using IslaTortuga.Server.Sessions;

namespace IslaTortuga.Server.Api;

public static class AuthEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth");

        auth.MapPost("/dev-login", (
            DevLoginRequest request,
            InMemorySessionCookieStore cookieStore,
            HttpContext httpContext) =>
        {
            var principal = cookieStore.Issue(
                string.IsNullOrWhiteSpace(request.UserId) ? Guid.NewGuid().ToString("N") : request.UserId,
                string.IsNullOrWhiteSpace(request.DisplayName) ? "DevPlayer" : request.DisplayName);

            httpContext.Response.Cookies.Append(
                CookieSessionValidator.CookieName,
                principal.SessionCookieId,
                new CookieOptions
                {
                    HttpOnly = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = false,
                    Expires = DateTimeOffset.UtcNow.AddDays(7),
                });

            return Results.Ok(new SessionInfoResponse(
                principal.SessionCookieId,
                principal.UserId,
                principal.DisplayName));
        });

        auth.MapGet("/session", async (
            HttpContext httpContext,
            ISessionCookieValidator sessionCookieValidator,
            CancellationToken cancellationToken) =>
        {
            var principal = await sessionCookieValidator.ValidateAsync(httpContext, cancellationToken);

            return principal is null
                ? Results.Unauthorized()
                : Results.Ok(new SessionInfoResponse(
                    principal.SessionCookieId,
                    principal.UserId,
                    principal.DisplayName));
        });

        auth.MapPost("/logout", (HttpContext httpContext, InMemorySessionCookieStore cookieStore) =>
        {
            if (httpContext.Request.Cookies.TryGetValue(CookieSessionValidator.CookieName, out var cookieValue))
            {
                cookieStore.Remove(cookieValue);
            }

            httpContext.Response.Cookies.Delete(CookieSessionValidator.CookieName);
            return Results.NoContent();
        });
    }

    private sealed record DevLoginRequest(string? UserId, string? DisplayName);

    private sealed record SessionInfoResponse(string SessionCookieId, string UserId, string DisplayName);
}
