using Konqvist.Infrastructure.Persistence;
using Konqvist.Server.Domain.Aggregates;
using Konqvist.Server.Features.Auth;
using Konqvist.Server.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Konqvist.Server.Features.Diagnostics;

public static class PhaseNavigationTestEndpoints
{
    public static IEndpointRouteBuilder MapPhaseNavigationTestEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/dev/phase-test")
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = AuthConstants.AuthenticationScheme
            });
        group.MapPost("/gathering/{gameSessionId:int}", BroadcastGatheringAsync);
        group.MapPost("/reset/{gameSessionId:int}", ResetSessionAsync);

        return endpoints;
    }

    private static async Task<IResult> BroadcastGatheringAsync(
        int gameSessionId,
        HttpContext httpContext,
        KonqvistDbContext dbContext,
        IHubContext<GameHub, IGameClient> hubContext,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("PhaseNavigationTestEndpoints");
        var playerSessionIdValue = httpContext.User.FindFirstValue(AuthConstants.ClaimTypes.PlayerSessionId);
        if (int.TryParse(playerSessionIdValue, out var playerSessionId))
        {
            var callerGameSessionId = await dbContext.PlayerSessions
                .AsNoTracking()
                .Where(entity => entity.Id == playerSessionId)
                .Select(entity => (int?)entity.GameSessionId)
                .SingleOrDefaultAsync(cancellationToken);
            if (!callerGameSessionId.HasValue || callerGameSessionId.Value != gameSessionId)
            {
                return Results.Forbid();
            }
        }
        else
        {
            var role = httpContext.User.FindFirstValue(ClaimTypes.Role);
            var callerGameSessionIdValue = httpContext.User.FindFirstValue(AuthConstants.ClaimTypes.GameSessionId);
            if (!string.Equals(role, Konqvist.Infrastructure.Entities.Enums.PlayerRole.GameMaster.ToString(), StringComparison.Ordinal)
                || !int.TryParse(callerGameSessionIdValue, out var callerGameSessionId)
                || callerGameSessionId != gameSessionId)
            {
                return Results.Unauthorized();
            }
        }

        var session = await dbContext.GameSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == gameSessionId, cancellationToken);
        if (session is null)
        {
            return Results.NotFound();
        }

        var message = new PhaseChangedMessage(
            session.Id,
            session.CurrentRoundSessionId,
            session.CurrentPhase.ToString(),
            Konqvist.Infrastructure.Entities.Enums.GamePhase.Gathering.ToString(),
            null,
            DateTime.UtcNow);

        await hubContext.Clients.Group(GameHubGroups.Game(session.Id)).PhaseChanged(message);
        logger.LogInformation(
            "Broadcasted development Gathering phase change for game session {GameSessionId}.",
            session.Id);

        return Results.NoContent();
    }

    private static async Task<IResult> ResetSessionAsync(
        int gameSessionId,
        HttpContext httpContext,
        KonqvistDbContext dbContext,
        GameAggregate gameAggregate,
        IHubContext<GameHub, IGameClient> hubContext,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("PhaseNavigationTestEndpoints");
        var role = httpContext.User.FindFirstValue(ClaimTypes.Role);
        var callerGameSessionIdValue = httpContext.User.FindFirstValue(AuthConstants.ClaimTypes.GameSessionId);
        if (!string.Equals(role, Konqvist.Infrastructure.Entities.Enums.PlayerRole.GameMaster.ToString(), StringComparison.Ordinal)
            || !int.TryParse(callerGameSessionIdValue, out var callerGameSessionId)
            || callerGameSessionId != gameSessionId)
        {
            return Results.Unauthorized();
        }

        GameAggregate.DevelopmentSessionResetResult result;
        try
        {
            result = await gameAggregate.ResetToWaitingForPlayersForDevelopmentAsync(gameSessionId, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }

        if (result.PreviousPhase != Konqvist.Infrastructure.Entities.Enums.GamePhase.WaitingForPlayers)
        {
            var message = new PhaseChangedMessage(
                result.GameSessionId,
                result.CurrentRoundSessionId,
                result.PreviousPhase.ToString(),
                result.CurrentPhase.ToString(),
                null,
                DateTime.UtcNow);

            await hubContext.Clients.Group(GameHubGroups.Game(result.GameSessionId)).PhaseChanged(message);
        }

        logger.LogInformation(
            "Reset development game session {GameSessionId} back to Pending/WaitingForPlayers for manual validation.",
            result.GameSessionId);

        return Results.NoContent();
    }
}
