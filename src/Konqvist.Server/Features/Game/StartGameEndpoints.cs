using System.Security.Claims;
using Konqvist.Infrastructure.Entities.Enums;
using Konqvist.Server.Domain.Aggregates;
using Konqvist.Server.Domain.Events;
using Konqvist.Server.Features.Auth;
using Konqvist.Server.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Konqvist.Server.Features.Game;

public static class StartGameEndpoints
{
    public static IEndpointRouteBuilder MapGameEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/game")
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = AuthConstants.AuthenticationScheme,
                Roles = nameof(PlayerRole.GameMaster)
            });

        group.MapPost("/start", StartAsync);

        return endpoints;
    }

    private static async Task<IResult> StartAsync(
        HttpContext httpContext,
        GameAggregate gameAggregate,
        IHubContext<GameHub, IGameClient> hubContext,
        CancellationToken cancellationToken)
    {
        var gameSessionId = GetGameSessionId(httpContext.User);
        if (!gameSessionId.HasValue)
        {
            return Results.Unauthorized();
        }

        GameAggregateCommandResult<GamePhaseChanged> result;
        try
        {
            result = await gameAggregate.StartGame(gameSessionId.Value, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new GameErrorResponse(ex.Message));
        }

        var message = new PhaseChangedMessage(
            result.PrimaryEvent.GameSessionId,
            result.PrimaryEvent.RoundSessionId,
            result.PrimaryEvent.PreviousPhase.ToString(),
            result.PrimaryEvent.CurrentPhase.ToString(),
            result.PrimaryEvent.ActorPlayerSessionId,
            result.PrimaryEvent.OccurredAt);

        await hubContext.Clients.Group(GameHubGroups.Game(result.PrimaryEvent.GameSessionId)).PhaseChanged(message);
        return Results.NoContent();
    }

    private static int? GetGameSessionId(ClaimsPrincipal user)
    {
        var claimValue = user.FindFirstValue(AuthConstants.ClaimTypes.GameSessionId);
        return int.TryParse(claimValue, out var gameSessionId) ? gameSessionId : null;
    }
}
