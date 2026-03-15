using System.Security.Claims;
using Konqvist.Infrastructure.Entities.Enums;
using Konqvist.Infrastructure.Persistence;
using Konqvist.Server.Features.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Konqvist.Server.Features.SessionState;

public static class SessionStateEndpoints
{
    public static IEndpointRouteBuilder MapSessionStateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/session")
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = AuthConstants.AuthenticationScheme
            });

        group.MapGet("/state", GetStateAsync);

        return endpoints;
    }

    private static async Task<IResult> GetStateAsync(
        HttpContext httpContext,
        KonqvistDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var playerSessionId = GetPlayerSessionId(httpContext.User);
        if (playerSessionId is null)
        {
            await httpContext.SignOutAsync(AuthConstants.AuthenticationScheme);
            return Results.Unauthorized();
        }

        var playerSession = await dbContext.PlayerSessions
            .Include(entity => entity.PlayerTemplate)
                .ThenInclude(entity => entity.TeamTemplate)
            .Include(entity => entity.GameSession)
                .ThenInclude(entity => entity.GameTemplate)
            .FirstOrDefaultAsync(entity => entity.Id == playerSessionId.Value, cancellationToken);
        if (playerSession is null || !playerSession.IsLoggedIn)
        {
            await httpContext.SignOutAsync(AuthConstants.AuthenticationScheme);
            return Results.Unauthorized();
        }

        var gameSession = await dbContext.GameSessions
            .Where(entity => entity.Id == playerSession.GameSessionId)
            .Include(entity => entity.GameTemplate)
            .Include(entity => entity.Teams)
                .ThenInclude(entity => entity.TeamTemplate)
            .Include(entity => entity.Districts)
            .Include(entity => entity.Players)
                .ThenInclude(entity => entity.PlayerTemplate)
            .Include(entity => entity.Rounds)
                .ThenInclude(entity => entity.RoundTemplate)
            .SingleOrDefaultAsync(cancellationToken);
        if (gameSession is null)
        {
            await httpContext.SignOutAsync(AuthConstants.AuthenticationScheme);
            return Results.Unauthorized();
        }

        var orderedRounds = gameSession.Rounds
            .OrderBy(entity => entity.RoundTemplate.RoundNumber)
            .ThenBy(entity => entity.Id)
            .ToList();
        if (orderedRounds.Count == 0)
        {
            return Results.Conflict(new AuthErrorResponse("The active game session has no rounds."));
        }

        var currentRound = gameSession.CurrentRoundSessionId.HasValue
            ? orderedRounds.Single(entity => entity.Id == gameSession.CurrentRoundSessionId.Value)
            : orderedRounds[0];

        var votesPerTeam = await dbContext.Votes
            .Where(entity => entity.RoundSessionId == currentRound.Id)
            .GroupBy(entity => entity.TargetTeamSessionId)
            .Select(group => new { TeamSessionId = group.Key, VoteTotal = group.Sum(entity => entity.VoteValue) })
            .ToDictionaryAsync(entity => entity.TeamSessionId, entity => entity.VoteTotal, cancellationToken);

        var timeRemaining = currentRound.VotingEnabled && currentRound.VotingStartedAt.HasValue
            ? (TimeSpan?)CalculateTimeRemaining(currentRound.VotingStartedAt.Value, gameSession.GameTemplate.VotingDurationSeconds)
            : null;

        var teamSession = gameSession.Teams
            .SingleOrDefault(entity => entity.TeamTemplateId == playerSession.PlayerTemplate.TeamTemplateId);

        var response = new SessionStateResponse(
            new SessionGameStateSnapshot(
                gameSession.CurrentPhase,
                currentRound.RoundTemplate.RoundNumber,
                gameSession.Id),
            new SessionMapStateSnapshot(
                gameSession.Districts.ToDictionary(entity => entity.Id, entity => entity.CurrentOwnerTeamSessionId),
                gameSession.Players
                    .Where(entity =>
                        entity.PlayerTemplate.Role == PlayerRole.Runner
                        && entity.IsLoggedIn
                        && entity.LocationLat.HasValue
                        && entity.LocationLng.HasValue
                        && (playerSession.PlayerTemplate.Role == PlayerRole.GameMaster
                            || entity.PlayerTemplate.TeamTemplateId == playerSession.PlayerTemplate.TeamTemplateId))
                    .ToDictionary(
                        entity => entity.Id,
                        entity => new SessionRunnerPosition(entity.LocationLat!.Value, entity.LocationLng!.Value))),
            new SessionVotingStateSnapshot(
                votesPerTeam,
                currentRound.VotingEnabled,
                timeRemaining),
            new SessionScoresStateSnapshot(
                gameSession.Teams.ToDictionary(entity => entity.Id, entity => entity.TotalScore),
                gameSession.Teams.ToDictionary(
                    entity => entity.Id,
                    entity => new SessionTeamResourceTotals(
                        entity.TotalGold,
                        entity.TotalVoters,
                        entity.TotalLikes,
                        entity.TotalOil))),
            new SessionPlayerStateSnapshot(
                playerSession.Id,
                teamSession?.Id,
                playerSession.PlayerTemplate.TeamTemplate.Name,
                playerSession.PlayerTemplate.Role,
                playerSession.IsLoggedIn,
                playerSession.IsOnline));

        return Results.Ok(response);
    }

    private static int? GetPlayerSessionId(ClaimsPrincipal user)
    {
        var claimValue = user.FindFirstValue(AuthConstants.ClaimTypes.PlayerSessionId);
        return int.TryParse(claimValue, out var playerSessionId) ? playerSessionId : null;
    }

    private static TimeSpan CalculateTimeRemaining(DateTime votingStartedAt, int votingDurationSeconds)
    {
        var elapsed = DateTime.UtcNow - votingStartedAt;
        var remaining = TimeSpan.FromSeconds(votingDurationSeconds) - elapsed;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }
}
