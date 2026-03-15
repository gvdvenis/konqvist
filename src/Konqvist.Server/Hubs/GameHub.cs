using System.Security.Claims;
using Konqvist.Infrastructure.Entities.Enums;
using Konqvist.Infrastructure.Persistence;
using Konqvist.Server.Domain.Aggregates;
using Konqvist.Server.Features.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Konqvist.Server.Hubs;

[Authorize(AuthenticationSchemes = AuthConstants.AuthenticationScheme)]
public sealed class GameHub(
    GameAggregate gameAggregate,
    IDbContextFactory<KonqvistDbContext> dbContextFactory,
    IPlayerConnectionTracker connectionTracker,
    ILogger<GameHub> logger) : Hub<IGameClient>
{
    private const string PlayerContextKey = "konqvist:hub_player_context";

    public override async Task OnConnectedAsync()
    {
        var player = await GetConnectedPlayerAsync(Context.ConnectionAborted, refresh: true)
            ?? throw new HubException("The current connection is not associated with a valid game session.");

        await Groups.AddToGroupAsync(Context.ConnectionId, GameHubGroups.Game(player.GameSessionId), Context.ConnectionAborted);
        foreach (var teamSessionId in player.SubscribedTeamSessionIds)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GameHubGroups.Team(player.GameSessionId, teamSessionId), Context.ConnectionAborted);
        }

        if (player.PlayerSessionId.HasValue)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GameHubGroups.Player(player.PlayerSessionId.Value), Context.ConnectionAborted);

            var isFirstConnection = connectionTracker.AddConnection(player.PlayerSessionId.Value, Context.ConnectionId);
            if (isFirstConnection)
            {
                var runnerStateChanged = await SetOnlineStateAsync(player, isOnline: true, Context.ConnectionAborted);
                if (runnerStateChanged is not null)
                {
                    await Clients.Group(GameHubGroups.Game(player.GameSessionId)).RunnerStateChanged(runnerStateChanged);
                }
            }
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var trackedPlayerSessionId = GetPlayerSessionId(Context.User);
            var isLastConnection = trackedPlayerSessionId.HasValue
                && connectionTracker.RemoveConnection(trackedPlayerSessionId.Value, Context.ConnectionId);

            var player = await GetConnectedPlayerAsync(
                CancellationToken.None,
                refresh: true,
                allowMissing: true,
                requireLoggedIn: false);
            if (player is not null && isLastConnection)
            {
                var runnerStateChanged = await SetOnlineStateAsync(player, isOnline: false, CancellationToken.None);
                if (player.PlayerSessionId.HasValue && connectionTracker.HasConnections(player.PlayerSessionId.Value))
                {
                    await SetOnlineStateAsync(player, isOnline: true, CancellationToken.None);
                    return;
                }

                if (runnerStateChanged is not null)
                {
                    await Clients.Group(GameHubGroups.Game(player.GameSessionId)).RunnerStateChanged(runnerStateChanged);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to finalize disconnect handling for connection {ConnectionId}.", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task ClaimDistrict(int districtId)
    {
        var player = await GetConnectedPlayerAsync(Context.ConnectionAborted, refresh: true)
            ?? throw new HubException("The current connection is not associated with a valid player session.");
        var playerSessionId = player.PlayerSessionId
            ?? throw new HubException("Only player sessions can claim districts.");

        try
        {
            var result = await gameAggregate.ClaimDistrict(playerSessionId, districtId, Context.ConnectionAborted);
            var message = new DistrictClaimedMessage(
                result.PrimaryEvent.GameSessionId,
                result.PrimaryEvent.RoundSessionId,
                result.PrimaryEvent.DistrictSessionId,
                result.PrimaryEvent.TeamSessionId,
                result.PrimaryEvent.ActorPlayerSessionId,
                result.PrimaryEvent.OccurredAt);

            if (result.WasIdempotent)
            {
                await Clients.Caller.DistrictClaimed(message);
                return;
            }

            await Clients.Group(GameHubGroups.Game(player.GameSessionId)).DistrictClaimed(message);
        }
        catch (InvalidOperationException ex)
        {
            throw new HubException(ex.Message);
        }
    }

    public async Task CastVote(int targetTeamId)
    {
        var player = await GetConnectedPlayerAsync(Context.ConnectionAborted, refresh: true)
            ?? throw new HubException("The current connection is not associated with a valid player session.");
        var playerSessionId = player.PlayerSessionId
            ?? throw new HubException("Only player sessions can cast votes.");

        try
        {
            var result = await gameAggregate.CastVote(playerSessionId, targetTeamId, cancellationToken: Context.ConnectionAborted);
            var message = new VoteCastMessage(
                result.PrimaryEvent.GameSessionId,
                result.PrimaryEvent.RoundSessionId,
                result.PrimaryEvent.VotingTeamSessionId,
                result.PrimaryEvent.TargetTeamSessionId,
                result.PrimaryEvent.ActorPlayerSessionId,
                result.PrimaryEvent.VoteValue,
                result.PrimaryEvent.OccurredAt);

            await Clients.Group(GameHubGroups.Game(player.GameSessionId)).VoteCast(message);
        }
        catch (InvalidOperationException ex)
        {
            throw new HubException(ex.Message);
        }
    }

    public async Task UpdateLocation(double lat, double lng)
    {
        var player = await GetConnectedPlayerAsync(Context.ConnectionAborted, refresh: true)
            ?? throw new HubException("The current connection is not associated with a valid player session.");
        var playerSessionId = player.PlayerSessionId
            ?? throw new HubException("Only player sessions can update locations.");
        var teamSessionId = player.TeamSessionId
            ?? throw new HubException("Only team-bound player sessions can update locations.");

        try
        {
            var result = await gameAggregate.UpdateLocation(playerSessionId, lat, lng, Context.ConnectionAborted);
            var message = new LocationUpdatedMessage(
                result.PrimaryEvent.GameSessionId,
                result.PrimaryEvent.RoundSessionId,
                result.PrimaryEvent.PlayerSessionId,
                result.PrimaryEvent.TeamSessionId,
                result.PrimaryEvent.Latitude,
                result.PrimaryEvent.Longitude,
                result.PrimaryEvent.OccurredAt);

            if (!result.PrimaryEvent.Accepted)
            {
                return;
            }

            await Clients.Group(GameHubGroups.Team(player.GameSessionId, teamSessionId)).LocationUpdated(message);

            if (!result.PrimaryEvent.BroadcastToOpponents)
            {
                return;
            }

            var opponentGroups = player.AllTeamSessionIds
                .Where(opponentTeamSessionId => opponentTeamSessionId != teamSessionId)
                .Select(opponentTeamSessionId => GameHubGroups.Team(player.GameSessionId, opponentTeamSessionId))
                .ToArray();
            if (opponentGroups.Length > 0)
            {
                await Clients.Groups(opponentGroups).LocationUpdated(message);
            }
        }
        catch (InvalidOperationException ex)
        {
            throw new HubException(ex.Message);
        }
    }

    private async Task<ConnectedPlayerContext?> GetConnectedPlayerAsync(
        CancellationToken cancellationToken,
        bool refresh = false,
        bool allowMissing = false,
        bool requireLoggedIn = true)
    {
        if (!refresh && Context.Items.TryGetValue(PlayerContextKey, out var cached) && cached is ConnectedPlayerContext cachedPlayer)
        {
            return cachedPlayer;
        }

        var playerSessionId = GetPlayerSessionId(Context.User);
        if (!playerSessionId.HasValue)
        {
            var role = GetRole(Context.User);
            if (role != PlayerRole.GameMaster)
            {
                if (allowMissing)
                {
                    return null;
                }

                throw new HubException("The current connection is not associated with a player session.");
            }

            var gameSessionId = GetGameSessionId(Context.User);
            if (!gameSessionId.HasValue)
            {
                if (allowMissing)
                {
                    return null;
                }

                throw new HubException("The current connection is not associated with a game session.");
            }

            await using var gameSessionDbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var gameSession = await gameSessionDbContext.GameSessions
                .Include(entity => entity.Teams)
                .SingleOrDefaultAsync(entity => entity.Id == gameSessionId.Value, cancellationToken);
            if (gameSession is null)
            {
                if (allowMissing)
                {
                    return null;
                }

                throw new HubException($"Game session '{gameSessionId.Value}' was not found.");
            }

            var connectedGameMaster = new ConnectedPlayerContext(
                null,
                gameSession.Id,
                null,
                PlayerRole.GameMaster,
                gameSession.Teams.Select(entity => entity.Id).ToArray(),
                gameSession.Teams.Select(entity => entity.Id).ToArray(),
                true);
            Context.Items[PlayerContextKey] = connectedGameMaster;
            return connectedGameMaster;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var playerSession = await dbContext.PlayerSessions
            .Include(entity => entity.PlayerTemplate)
            .Include(entity => entity.GameSession)
                .ThenInclude(entity => entity.Teams)
            .SingleOrDefaultAsync(entity => entity.Id == playerSessionId.Value, cancellationToken);
        if (playerSession is null)
        {
            if (allowMissing)
            {
                return null;
            }

            throw new HubException($"Player session '{playerSessionId.Value}' was not found.");
        }

        if (requireLoggedIn && !playerSession.IsLoggedIn)
        {
            if (allowMissing)
            {
                return null;
            }

            throw new HubException($"Player session '{playerSession.Id}' is not logged in.");
        }

        var teamSession = playerSession.GameSession.Teams
            .SingleOrDefault(entity => entity.TeamTemplateId == playerSession.PlayerTemplate.TeamTemplateId);
        if (teamSession is null)
        {
            if (allowMissing)
            {
                return null;
            }

            throw new HubException($"Player session '{playerSession.Id}' is not associated with an active team session.");
        }

        var connectedPlayer = new ConnectedPlayerContext(
            playerSession.Id,
            playerSession.GameSessionId,
            teamSession.Id,
            playerSession.PlayerTemplate.Role,
            playerSession.GameSession.Teams.Select(entity => entity.Id).ToArray(),
            [teamSession.Id],
            playerSession.IsLoggedIn);
        Context.Items[PlayerContextKey] = connectedPlayer;
        return connectedPlayer;
    }

    private async Task<RunnerStateChangedMessage?> SetOnlineStateAsync(
        ConnectedPlayerContext player,
        bool isOnline,
        CancellationToken cancellationToken)
    {
        var playerSessionId = player.PlayerSessionId
            ?? throw new InvalidOperationException("Online state updates require a player session.");
        var teamSessionId = player.TeamSessionId
            ?? throw new InvalidOperationException("Online state updates require a team session.");

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var playerSession = await dbContext.PlayerSessions
            .SingleOrDefaultAsync(entity => entity.Id == playerSessionId, cancellationToken);
        if (playerSession is null)
        {
            return null;
        }

        var occurredAt = DateTime.UtcNow;
        playerSession.IsOnline = isOnline;
        playerSession.LastSeen = occurredAt;
        await dbContext.SaveChangesAsync(cancellationToken);

        if (player.Role != PlayerRole.Runner)
        {
            return null;
        }

        return new RunnerStateChangedMessage(
            player.GameSessionId,
            playerSessionId,
            teamSessionId,
            player.IsLoggedIn,
            isOnline,
            playerSession.LastSeen,
            occurredAt);
    }

    private static int? GetPlayerSessionId(ClaimsPrincipal? principal)
    {
        var claim = principal?.FindFirst(AuthConstants.ClaimTypes.PlayerSessionId)?.Value;
        return int.TryParse(claim, out var playerSessionId) ? playerSessionId : null;
    }

    private static int? GetGameSessionId(ClaimsPrincipal? principal)
    {
        var claim = principal?.FindFirst(AuthConstants.ClaimTypes.GameSessionId)?.Value;
        return int.TryParse(claim, out var gameSessionId) ? gameSessionId : null;
    }

    private static PlayerRole? GetRole(ClaimsPrincipal? principal)
    {
        var claim = principal?.FindFirst(ClaimTypes.Role)?.Value;
        return Enum.TryParse<PlayerRole>(claim, out var role) ? role : null;
    }

    private sealed record ConnectedPlayerContext(
        int? PlayerSessionId,
        int GameSessionId,
        int? TeamSessionId,
        PlayerRole Role,
        IReadOnlyList<int> AllTeamSessionIds,
        IReadOnlyList<int> SubscribedTeamSessionIds,
        bool IsLoggedIn);
}
