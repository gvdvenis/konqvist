using System.Security.Claims;
using Konqvist.Infrastructure.Entities.Enums;
using Konqvist.Infrastructure.Persistence;
using Konqvist.Server.Domain.Aggregates;
using Konqvist.Server.Domain.Events;
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
            ?? throw new HubException("The current connection is not associated with a valid player session.");

        await Groups.AddToGroupAsync(Context.ConnectionId, GameHubGroups.Game(player.GameSessionId), Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, GameHubGroups.Team(player.GameSessionId, player.TeamSessionId), Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, GameHubGroups.Player(player.PlayerSessionId), Context.ConnectionAborted);

        var isFirstConnection = connectionTracker.AddConnection(player.PlayerSessionId, Context.ConnectionId);
        if (isFirstConnection)
        {
            var runnerStateChanged = await SetOnlineStateAsync(player, isOnline: true, Context.ConnectionAborted);
            if (runnerStateChanged is not null)
            {
                await Clients.Group(GameHubGroups.Game(player.GameSessionId)).RunnerStateChanged(runnerStateChanged);
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
                if (connectionTracker.HasConnections(player.PlayerSessionId))
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

        try
        {
            var result = await gameAggregate.ClaimDistrict(player.PlayerSessionId, districtId, Context.ConnectionAborted);
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

        try
        {
            var result = await gameAggregate.CastVote(player.PlayerSessionId, targetTeamId, cancellationToken: Context.ConnectionAborted);
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

        try
        {
            var result = await gameAggregate.UpdateLocation(player.PlayerSessionId, lat, lng, Context.ConnectionAborted);
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

            await Clients.Group(GameHubGroups.Team(player.GameSessionId, player.TeamSessionId)).LocationUpdated(message);

            if (!result.PrimaryEvent.BroadcastToOpponents)
            {
                return;
            }

            var opponentGroups = player.AllTeamSessionIds
                .Where(teamSessionId => teamSessionId != player.TeamSessionId)
                .Select(teamSessionId => GameHubGroups.Team(player.GameSessionId, teamSessionId))
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
            if (allowMissing)
            {
                return null;
            }

            throw new HubException("The current connection is not associated with a player session.");
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
            playerSession.IsLoggedIn);
        Context.Items[PlayerContextKey] = connectedPlayer;
        return connectedPlayer;
    }

    private async Task<RunnerStateChangedMessage?> SetOnlineStateAsync(
        ConnectedPlayerContext player,
        bool isOnline,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var playerSession = await dbContext.PlayerSessions
            .SingleOrDefaultAsync(entity => entity.Id == player.PlayerSessionId, cancellationToken);
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
            player.PlayerSessionId,
            player.TeamSessionId,
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

    private sealed record ConnectedPlayerContext(
        int PlayerSessionId,
        int GameSessionId,
        int TeamSessionId,
        PlayerRole Role,
        IReadOnlyList<int> AllTeamSessionIds,
        bool IsLoggedIn);
}
