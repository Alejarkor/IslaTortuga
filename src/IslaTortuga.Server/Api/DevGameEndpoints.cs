using IslaTortuga.Server.Rooms;
using IslaTortuga.Server.Sessions;

namespace IslaTortuga.Server.Api;

public static class DevGameEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        var game = api.MapGroup("/game");

        game.MapPost("/ticket", async (
            HttpContext httpContext,
            ISessionCookieValidator sessionCookieValidator,
            GameTicketService gameTicketService,
            CancellationToken cancellationToken) =>
        {
            var principal = await sessionCookieValidator.ValidateAsync(httpContext, cancellationToken);
            if (principal is null)
            {
                return Results.Unauthorized();
            }

            var ticket = gameTicketService.CreateJoinTicket(principal);
            return Results.Ok(ToResponse(ticket));
        });

        game.MapPost("/reconnect-ticket", async (
            ReconnectTicketRequest request,
            HttpContext httpContext,
            ISessionCookieValidator sessionCookieValidator,
            GameTicketService gameTicketService,
            CancellationToken cancellationToken) =>
        {
            var principal = await sessionCookieValidator.ValidateAsync(httpContext, cancellationToken);
            if (principal is null)
            {
                return Results.Unauthorized();
            }

            var ticket = gameTicketService.CreateReconnectTicket(principal, request.PreviousSessionId);
            return Results.Ok(ToResponse(ticket));
        });

        game.MapGet("/dev/rooms", (GameRoomManager roomManager) =>
        {
            var rooms = roomManager.GetAllRooms()
                .Select(room => new RoomSummaryResponse(
                    room.RoomId,
                    room.State.ToString(),
                    room.World.SceneData.DisplayName,
                    room.Players.Count))
                .ToArray();

            return Results.Ok(rooms);
        });
    }

    private static GameTicketResponse ToResponse(GameTicket ticket) =>
        new(
            ticket.TicketId,
            ticket.Purpose.ToString(),
            ticket.ExpiresAt,
            ticket.PreviousSessionId);

    private sealed record ReconnectTicketRequest(string? PreviousSessionId);

    private sealed record GameTicketResponse(
        string GameTicket,
        string Purpose,
        DateTimeOffset ExpiresAt,
        string? PreviousSessionId);

    private sealed record RoomSummaryResponse(
        string RoomId,
        string State,
        string SceneName,
        int PlayerCount);
}
