using Konqvist.Infrastructure.Entities.Enums;
using Konqvist.Infrastructure.Persistence;
using Konqvist.Server.Domain.Events;
using Konqvist.Server.Domain.Persistence;
using Konqvist.Server.Domain.Serialization;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace Konqvist.Server.Domain.Aggregates;

public sealed class GameAggregate(
    IDbContextFactory<KonqvistDbContext> dbContextFactory,
    IGameEventRepository gameEventRepository)
{
    private static readonly IReadOnlyDictionary<int, int> EmptyScores = new ConcurrentDictionary<int, int>();
    private static readonly IReadOnlyDictionary<int, int?> EmptyDistrictOwnership = new ConcurrentDictionary<int, int?>();

    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _locationMutexes = [];
    private AggregateState? _state;
    private int _stateGeneration;

    public GamePhase CurrentPhase => _state?.CurrentPhase ?? GamePhase.WaitingForPlayers;

    public int CurrentRoundNumber => _state?.CurrentRoundNumber ?? 0;

    public IReadOnlyDictionary<int, int> TeamScores => _state?.TeamScores ?? EmptyScores;

    public IReadOnlyDictionary<int, int?> DistrictOwnership => _state?.DistrictOwnership ?? EmptyDistrictOwnership;

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            _state = null;
            _locationMutexes.Clear();
            await EnsureInitializedAsync(cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<DevelopmentSessionResetResult> ResetToWaitingForPlayersForDevelopmentAsync(
        int gameSessionId,
        CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var state = await EnsureInitializedAsync(cancellationToken);
            if (state.GameSessionId != gameSessionId)
            {
                throw new InvalidOperationException($"Game session '{gameSessionId}' is not the active session.");
            }

            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var session = await dbContext.GameSessions
                .Include(entity => entity.Teams)
                .Include(entity => entity.Districts)
                .Include(entity => entity.Players)
                .Include(entity => entity.Rounds)
                    .ThenInclude(entity => entity.RoundTemplate)
                .Include(entity => entity.Rounds)
                    .ThenInclude(entity => entity.Votes)
                .SingleOrDefaultAsync(entity => entity.Id == gameSessionId, cancellationToken)
                ?? throw new InvalidOperationException("The active game session could not be found.");

            var orderedRounds = session.Rounds
                .OrderBy(entity => entity.RoundTemplate.RoundNumber)
                .ThenBy(entity => entity.Id)
                .ToList();
            if (orderedRounds.Count == 0)
            {
                throw new InvalidOperationException("The active game session has no rounds.");
            }

            var previousPhase = session.CurrentPhase;
            _stateGeneration++;

            session.Status = GameStatus.Pending;
            session.CurrentPhase = GamePhase.WaitingForPlayers;
            session.StartedAt = null;
            session.FinishedAt = null;
            session.CurrentRoundSessionId = orderedRounds[0].Id;

            foreach (var team in session.Teams)
            {
                team.TotalScore = 0;
                team.TotalGold = 0;
                team.TotalVoters = 0;
                team.TotalLikes = 0;
                team.TotalOil = 0;
            }

            foreach (var district in session.Districts)
            {
                district.CurrentOwnerTeamSessionId = null;
                district.IsClaimedThisRound = false;
                district.LastClaimedAt = null;
            }

            foreach (var player in session.Players)
            {
                player.IsOnline = false;
                player.LastSeen = null;
                player.LocationLat = null;
                player.LocationLng = null;
                player.LocationUpdatedAt = null;
            }

            foreach (var round in orderedRounds)
            {
                round.Status = RoundStatus.Gathering;
                round.VotingEnabled = false;
                round.VotingStartedAt = null;
                round.WinnerTeamSessionId = null;
            }

            dbContext.Votes.RemoveRange(orderedRounds.SelectMany(entity => entity.Votes));
            dbContext.GameEvents.RemoveRange(dbContext.GameEvents.Where(entity => entity.GameSessionId == session.Id));
            dbContext.RoundSnapshots.RemoveRange(dbContext.RoundSnapshots.Where(entity => entity.RoundSession.GameSessionId == session.Id));
            dbContext.DistrictOwnershipSnapshots.RemoveRange(dbContext.DistrictOwnershipSnapshots.Where(entity => entity.RoundSession.GameSessionId == session.Id));

            await dbContext.SaveChangesAsync(cancellationToken);

            state.CurrentPhase = GamePhase.WaitingForPlayers;
            state.CurrentRoundSessionId = orderedRounds[0].Id;
            state.CurrentRoundNumber = orderedRounds[0].RoundTemplate.RoundNumber;
            state.VotingEnabled = false;
            state.DistrictClaimsThisRound.Clear();
            state.TeamsThatVotedThisRound.Clear();
            state.LastLocationUpdateAt.Clear();
            state.LastOpponentLocationBroadcastAt.Clear();

            foreach (var team in session.Teams)
            {
                state.TeamScores[team.Id] = 0;
            }

            foreach (var district in session.Districts)
            {
                state.DistrictOwnership[district.Id] = null;
            }

            return new DevelopmentSessionResetResult(
                session.Id,
                state.CurrentRoundSessionId,
                previousPhase,
                state.CurrentPhase);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<GameAggregateCommandResult<GamePhaseChanged>> StartGame(
        int gameSessionId,
        CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var state = await EnsureInitializedAsync(cancellationToken);
            if (state.GameSessionId != gameSessionId)
            {
                throw new InvalidOperationException($"Game session '{gameSessionId}' is not the active session.");
            }

            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var session = await dbContext.GameSessions
                .Include(entity => entity.Rounds)
                    .ThenInclude(entity => entity.RoundTemplate)
                .SingleOrDefaultAsync(entity => entity.Id == state.GameSessionId, cancellationToken)
                ?? throw new InvalidOperationException("The active game session could not be found.");

            var canRecoverLegacyStartedSession =
                session.Status == GameStatus.Running &&
                session.CurrentPhase == GamePhase.WaitingForPlayers &&
                state.CurrentPhase == GamePhase.WaitingForPlayers;

            if (session.Status != GameStatus.Pending && !canRecoverLegacyStartedSession)
            {
                throw new InvalidOperationException("The active game session has already been started.");
            }

            var occurredAt = DateTime.UtcNow;
            var phaseChanged = CreatePhaseChangedEvent(state, GamePhase.Gathering, actorPlayerSessionId: null, occurredAt)
                ?? throw new InvalidOperationException("The active game session is already in the Gathering phase.");

            session.Status = GameStatus.Running;
            session.StartedAt ??= occurredAt;
            session.CurrentPhase = GamePhase.Gathering;
            session.CurrentRoundSessionId ??= state.CurrentRoundSessionId;

            dbContext.GameEvents.Add(new Infrastructure.Entities.Session.GameEvent
            {
                GameSessionId = phaseChanged.GameSessionId,
                RoundSessionId = phaseChanged.RoundSessionId,
                EventType = phaseChanged.EventType,
                Payload = GameEventPayloadSerializer.Serialize(phaseChanged),
                OccurredAt = phaseChanged.OccurredAt,
                ActorPlayerSessionId = phaseChanged.ActorPlayerSessionId
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            Apply(phaseChanged, state);

            return new GameAggregateCommandResult<GamePhaseChanged>(phaseChanged, [phaseChanged]);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<GameAggregateCommandResult<DistrictClaimed>> ClaimDistrict(
        int actorPlayerSessionId,
        int districtSessionId,
        CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var state = await EnsureInitializedAsync(cancellationToken);
            var teamSessionId = GetActorTeamSessionId(state, actorPlayerSessionId);
            EnsureActorRole(state, actorPlayerSessionId, PlayerRole.Runner, "claim a district");

            if (!state.DistrictOwnership.ContainsKey(districtSessionId))
            {
                throw new InvalidOperationException($"District session '{districtSessionId}' does not exist in the active game.");
            }

            if (state.DistrictOwnership[districtSessionId] == teamSessionId &&
                state.DistrictClaimsThisRound.TryGetValue(districtSessionId, out var previousClaim))
            {
                return new GameAggregateCommandResult<DistrictClaimed>(
                    previousClaim,
                    [previousClaim],
                    WasIdempotent: true);
            }

            var gameEvent = new DistrictClaimed(
                state.GameSessionId,
                state.CurrentRoundSessionId,
                districtSessionId,
                teamSessionId,
                actorPlayerSessionId,
                DateTime.UtcNow);

            await gameEventRepository.AppendAsync([gameEvent], cancellationToken);
            Apply(gameEvent, state);

            return new GameAggregateCommandResult<DistrictClaimed>(gameEvent, [gameEvent]);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<GameAggregateCommandResult<VoteCast>> CastVote(
        int actorPlayerSessionId,
        int targetTeamSessionId,
        int voteValue = 1,
        CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var state = await EnsureInitializedAsync(cancellationToken);
            var votingTeamSessionId = GetActorTeamSessionId(state, actorPlayerSessionId);
            EnsureActorRole(state, actorPlayerSessionId, PlayerRole.TeamLeader, "cast a vote");

            if (!state.VotingEnabled || state.CurrentPhase != GamePhase.Voting)
            {
                throw new InvalidOperationException("Voting is not currently open.");
            }

            if (!state.TeamScores.ContainsKey(targetTeamSessionId))
            {
                throw new InvalidOperationException($"Target team session '{targetTeamSessionId}' does not exist in the active game.");
            }

            if (state.TeamsThatVotedThisRound.Contains(votingTeamSessionId))
            {
                throw new InvalidOperationException($"Team session '{votingTeamSessionId}' has already voted in this round.");
            }

            var gameEvent = new VoteCast(
                state.GameSessionId,
                state.CurrentRoundSessionId,
                votingTeamSessionId,
                targetTeamSessionId,
                actorPlayerSessionId,
                voteValue,
                DateTime.UtcNow);

            await gameEventRepository.AppendAsync([gameEvent], cancellationToken);
            Apply(gameEvent, state);

            return new GameAggregateCommandResult<VoteCast>(gameEvent, [gameEvent]);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<GameAggregateCommandResult<LocationUpdated>> UpdateLocation(
        int actorPlayerSessionId,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        var locationMutex = _locationMutexes.GetOrAdd(actorPlayerSessionId, _ => new SemaphoreSlim(1, 1));
        await locationMutex.WaitAsync(cancellationToken);
        try
        {
            AggregateState state;
            int teamSessionId;
            int roundSessionId;
            DateTime occurredAt;
            DateTime? lastLocationUpdateAt;
            DateTime? lastBroadcastAt;
            bool isThrottled;
            bool shouldBroadcastToOpponents;
            int stateGeneration;

            await _mutex.WaitAsync(cancellationToken);
            try
            {
                state = await EnsureInitializedAsync(cancellationToken);
                teamSessionId = GetActorTeamSessionId(state, actorPlayerSessionId);
                roundSessionId = state.CurrentRoundSessionId;
                EnsureActorRole(state, actorPlayerSessionId, PlayerRole.Runner, "update location");

                if (state.CurrentPhase != GamePhase.Gathering)
                {
                    var ignoredLocationUpdated = new LocationUpdated(
                        state.GameSessionId,
                        roundSessionId,
                        actorPlayerSessionId,
                        teamSessionId,
                        latitude,
                        longitude,
                        Accepted: false,
                        BroadcastToOpponents: false,
                        DateTime.UtcNow);

                    return new GameAggregateCommandResult<LocationUpdated>(ignoredLocationUpdated, [], WasIdempotent: true);
                }

                occurredAt = DateTime.UtcNow;
                lastLocationUpdateAt = state.LastLocationUpdateAt.TryGetValue(actorPlayerSessionId, out var trackedLastLocationUpdateAt)
                    ? trackedLastLocationUpdateAt
                    : null;
                lastBroadcastAt = state.LastOpponentLocationBroadcastAt.TryGetValue(actorPlayerSessionId, out var trackedLastBroadcastAt)
                    ? trackedLastBroadcastAt
                    : null;
                isThrottled = lastLocationUpdateAt.HasValue
                    && occurredAt - lastLocationUpdateAt.Value < TimeSpan.FromSeconds(state.MinLocationUpdateIntervalSeconds);
                shouldBroadcastToOpponents = !lastBroadcastAt.HasValue
                    || occurredAt - lastBroadcastAt.Value >= TimeSpan.FromSeconds(state.LocationBroadcastIntervalSeconds);
                stateGeneration = _stateGeneration;
            }
            finally
            {
                _mutex.Release();
            }

            if (isThrottled)
            {
                var throttledLocationUpdated = new LocationUpdated(
                    state.GameSessionId,
                    roundSessionId,
                    actorPlayerSessionId,
                    teamSessionId,
                    latitude,
                    longitude,
                    Accepted: false,
                    BroadcastToOpponents: false,
                    occurredAt);

                return new GameAggregateCommandResult<LocationUpdated>(throttledLocationUpdated, [], WasIdempotent: true);
            }

            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var playerSession = await dbContext.PlayerSessions
                .SingleOrDefaultAsync(
                    entity => entity.Id == actorPlayerSessionId && entity.GameSessionId == state.GameSessionId,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Player session '{actorPlayerSessionId}' does not exist in the active game.");

            playerSession.LocationLat = latitude;
            playerSession.LocationLng = longitude;
            playerSession.LocationUpdatedAt = occurredAt;
            playerSession.LastSeen = occurredAt;

            await dbContext.SaveChangesAsync(cancellationToken);

            await _mutex.WaitAsync(cancellationToken);
            try
            {
                if (_stateGeneration != stateGeneration)
                {
                    playerSession.LocationLat = null;
                    playerSession.LocationLng = null;
                    playerSession.LocationUpdatedAt = null;
                    playerSession.LastSeen = null;
                    await dbContext.SaveChangesAsync(CancellationToken.None);

                    var supersededLocationUpdated = new LocationUpdated(
                        state.GameSessionId,
                        roundSessionId,
                        actorPlayerSessionId,
                        teamSessionId,
                        latitude,
                        longitude,
                        Accepted: false,
                        BroadcastToOpponents: false,
                        occurredAt);

                    return new GameAggregateCommandResult<LocationUpdated>(supersededLocationUpdated, [], WasIdempotent: true);
                }

                roundSessionId = state.CurrentRoundSessionId;
                state.LastLocationUpdateAt[actorPlayerSessionId] = occurredAt;
                if (shouldBroadcastToOpponents)
                {
                    state.LastOpponentLocationBroadcastAt[actorPlayerSessionId] = occurredAt;
                }
            }
            finally
            {
                _mutex.Release();
            }

            var locationUpdated = new LocationUpdated(
                state.GameSessionId,
                roundSessionId,
                actorPlayerSessionId,
                teamSessionId,
                latitude,
                longitude,
                Accepted: true,
                shouldBroadcastToOpponents,
                occurredAt);

            return new GameAggregateCommandResult<LocationUpdated>(locationUpdated, []);
        }
        finally
        {
            locationMutex.Release();
        }
    }

    public async Task<GameAggregateCommandResult<VotingOpened>> OpenVoting(
        int? actorPlayerSessionId = null,
        CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var state = await EnsureInitializedAsync(cancellationToken);
            if (state.VotingEnabled && state.CurrentPhase == GamePhase.Voting)
            {
                var existing = new VotingOpened(state.GameSessionId, state.CurrentRoundSessionId, actorPlayerSessionId, DateTime.UtcNow);
                return new GameAggregateCommandResult<VotingOpened>(existing, [], WasIdempotent: true);
            }

            var votingOpened = new VotingOpened(state.GameSessionId, state.CurrentRoundSessionId, actorPlayerSessionId, DateTime.UtcNow);
            var phaseChanged = CreatePhaseChangedEvent(state, GamePhase.Voting, actorPlayerSessionId, votingOpened.OccurredAt);
            var persistedEvents = BuildPersistedEvents(votingOpened, phaseChanged);

            await gameEventRepository.AppendAsync(persistedEvents, cancellationToken);
            Apply(votingOpened, state);
            if (phaseChanged is not null)
            {
                Apply(phaseChanged, state);
            }

            return new GameAggregateCommandResult<VotingOpened>(votingOpened, persistedEvents);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<GameAggregateCommandResult<VotingClosed>> CloseVoting(
        int? actorPlayerSessionId = null,
        CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var state = await EnsureInitializedAsync(cancellationToken);
            if (!state.VotingEnabled || state.CurrentPhase != GamePhase.Voting)
            {
                throw new InvalidOperationException("Voting is not currently open.");
            }

            var votingClosed = new VotingClosed(state.GameSessionId, state.CurrentRoundSessionId, actorPlayerSessionId, DateTime.UtcNow);
            var phaseChanged = CreatePhaseChangedEvent(state, GamePhase.RoundResolution, actorPlayerSessionId, votingClosed.OccurredAt);
            var persistedEvents = BuildPersistedEvents(votingClosed, phaseChanged);

            await gameEventRepository.AppendAsync(persistedEvents, cancellationToken);
            Apply(votingClosed, state);
            if (phaseChanged is not null)
            {
                Apply(phaseChanged, state);
            }

            return new GameAggregateCommandResult<VotingClosed>(votingClosed, persistedEvents);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<GameAggregateCommandResult<RoundAdvanced>> AdvanceRound(
        int? actorPlayerSessionId = null,
        CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var state = await EnsureInitializedAsync(cancellationToken);
            if (state.CurrentPhase == GamePhase.Finished)
            {
                throw new InvalidOperationException("The game is already finished.");
            }

            var nextRoundIndex = state.OrderedRoundSessionIds.IndexOf(state.CurrentRoundSessionId) + 1;
            var nextRoundSessionId = nextRoundIndex < state.OrderedRoundSessionIds.Count
                ? state.OrderedRoundSessionIds[nextRoundIndex]
                : (int?)null;
            var phaseAfterAdvance = nextRoundSessionId.HasValue ? GamePhase.Gathering : GamePhase.Finished;

            var roundAdvanced = new RoundAdvanced(
                state.GameSessionId,
                state.CurrentRoundSessionId,
                nextRoundSessionId,
                state.CurrentRoundNumber,
                nextRoundSessionId.HasValue ? state.RoundNumbers[nextRoundSessionId.Value] : null,
                phaseAfterAdvance,
                actorPlayerSessionId,
                DateTime.UtcNow);

            var phaseChanged = CreatePhaseChangedEvent(state, phaseAfterAdvance, actorPlayerSessionId, roundAdvanced.OccurredAt);
            var persistedEvents = BuildPersistedEvents(roundAdvanced, phaseChanged);

            await gameEventRepository.AppendAsync(persistedEvents, cancellationToken);
            Apply(roundAdvanced, state);
            if (phaseChanged is not null)
            {
                Apply(phaseChanged, state);
            }

            return new GameAggregateCommandResult<RoundAdvanced>(roundAdvanced, persistedEvents);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<GameAggregateCommandResult<RunnerLogout>> ForceLogoutRunner(
        int targetPlayerSessionId,
        int? actorPlayerSessionId = null,
        CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var state = await EnsureInitializedAsync(cancellationToken);
            if (!state.PlayerLoggedIn.ContainsKey(targetPlayerSessionId))
            {
                throw new InvalidOperationException($"Player session '{targetPlayerSessionId}' does not exist in the active game.");
            }

            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var playerSession = await dbContext.PlayerSessions
                .SingleOrDefaultAsync(
                    entity => entity.Id == targetPlayerSessionId && entity.GameSessionId == state.GameSessionId,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Player session '{targetPlayerSessionId}' does not exist in the active game.");

            state.PlayerLoggedIn[targetPlayerSessionId] = playerSession.IsLoggedIn;

            var gameEvent = new RunnerLogout(
                state.GameSessionId,
                targetPlayerSessionId,
                actorPlayerSessionId,
                DateTime.UtcNow);

            if (!state.PlayerLoggedIn[targetPlayerSessionId])
            {
                return new GameAggregateCommandResult<RunnerLogout>(gameEvent, [], WasIdempotent: true);
            }

            await gameEventRepository.AppendAsync([gameEvent], cancellationToken);
            playerSession.IsLoggedIn = false;
            await dbContext.SaveChangesAsync(cancellationToken);
            Apply(gameEvent, state);

            return new GameAggregateCommandResult<RunnerLogout>(gameEvent, [gameEvent]);
        }
        finally
        {
            _mutex.Release();
        }
    }

    private static IReadOnlyList<IGameDomainEvent> BuildPersistedEvents<TPrimaryEvent>(
        TPrimaryEvent primaryEvent,
        GamePhaseChanged? phaseChanged)
        where TPrimaryEvent : IGameDomainEvent
    {
        return phaseChanged is null
            ? [primaryEvent]
            : [primaryEvent, phaseChanged];
    }

    private static int GetActorTeamSessionId(AggregateState state, int actorPlayerSessionId)
    {
        if (!state.PlayerTeamSessionMap.TryGetValue(actorPlayerSessionId, out var teamSessionId))
        {
            throw new InvalidOperationException($"Player session '{actorPlayerSessionId}' does not exist in the active game.");
        }

        return teamSessionId;
    }

    private static void EnsureActorRole(
        AggregateState state,
        int actorPlayerSessionId,
        PlayerRole requiredRole,
        string action)
    {
        if (!state.PlayerRoles.TryGetValue(actorPlayerSessionId, out var actualRole))
        {
            throw new InvalidOperationException($"Player session '{actorPlayerSessionId}' does not exist in the active game.");
        }

        if (actualRole != requiredRole)
        {
            throw new InvalidOperationException(
                $"Player session '{actorPlayerSessionId}' must be a {requiredRole} to {action}.");
        }
    }

    private static GamePhaseChanged? CreatePhaseChangedEvent(
        AggregateState state,
        GamePhase nextPhase,
        int? actorPlayerSessionId,
        DateTime occurredAt)
    {
        if (state.CurrentPhase == nextPhase)
        {
            return null;
        }

        return new GamePhaseChanged(
            state.GameSessionId,
            state.CurrentRoundSessionId,
            state.CurrentPhase,
            nextPhase,
            actorPlayerSessionId,
            occurredAt);
    }

    private static void Apply(IGameDomainEvent gameEvent, AggregateState state)
    {
        switch (gameEvent)
        {
            case DistrictClaimed districtClaimed:
                state.DistrictOwnership[districtClaimed.DistrictSessionId] = districtClaimed.TeamSessionId;
                state.DistrictClaimsThisRound[districtClaimed.DistrictSessionId] = districtClaimed;
                break;
            case VoteCast voteCast:
                state.TeamsThatVotedThisRound.Add(voteCast.VotingTeamSessionId);
                break;
            case VotingOpened:
                state.VotingEnabled = true;
                break;
            case VotingClosed:
                state.VotingEnabled = false;
                break;
            case RoundAdvanced roundAdvanced:
                if (roundAdvanced.NextRoundSessionId.HasValue)
                {
                    state.CurrentRoundSessionId = roundAdvanced.NextRoundSessionId.Value;
                    state.CurrentRoundNumber = roundAdvanced.NextRoundNumber ?? state.CurrentRoundNumber;
                    state.TeamsThatVotedThisRound.Clear();
                    state.DistrictClaimsThisRound.Clear();
                    state.VotingEnabled = false;
                }
                else
                {
                    state.VotingEnabled = false;
                }

                break;
            case RunnerLogout runnerLogout:
                state.PlayerLoggedIn[runnerLogout.TargetPlayerSessionId] = false;
                break;
            case GamePhaseChanged phaseChanged:
                state.CurrentPhase = phaseChanged.CurrentPhase;
                break;
        }
    }

    private async Task<AggregateState> EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_state is not null && _state.CurrentPhase != GamePhase.Finished)
        {
            return _state;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var activeSessionId = await dbContext.GameSessions
            .Where(entity => entity.Status == GameStatus.Pending || entity.Status == GameStatus.Running)
            .OrderByDescending(entity => entity.Id)
            .Select(entity => entity.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeSessionId == 0)
        {
            throw new InvalidOperationException("No active game session exists for the aggregate to load.");
        }

        var session = await dbContext.GameSessions
            .Include(entity => entity.GameTemplate)
            .Include(entity => entity.Teams)
            .Include(entity => entity.Districts)
            .Include(entity => entity.Players)
                .ThenInclude(entity => entity.PlayerTemplate)
            .Include(entity => entity.Rounds)
                .ThenInclude(entity => entity.RoundTemplate)
            .Include(entity => entity.Rounds)
                .ThenInclude(entity => entity.Votes)
            .SingleAsync(entity => entity.Id == activeSessionId, cancellationToken);
        var persistedEvents = await dbContext.GameEvents
            .Where(entity => entity.GameSessionId == activeSessionId)
            .OrderBy(entity => entity.Id)
            .ToListAsync(cancellationToken);

        var orderedRounds = session.Rounds
            .OrderBy(entity => entity.RoundTemplate.RoundNumber)
            .ThenBy(entity => entity.Id)
            .ToList();

        if (orderedRounds.Count == 0)
        {
            throw new InvalidOperationException("The active game session has no rounds.");
        }

        var currentRound = session.CurrentRoundSessionId.HasValue
            ? orderedRounds.Single(entity => entity.Id == session.CurrentRoundSessionId.Value)
            : orderedRounds[0];

        var teamSessionByTemplateId = session.Teams.ToDictionary(entity => entity.TeamTemplateId, entity => entity.Id);
        var state = new AggregateState
        {
            GameSessionId = session.Id,
            CurrentPhase = session.CurrentPhase,
            CurrentRoundSessionId = currentRound.Id,
            CurrentRoundNumber = currentRound.RoundTemplate.RoundNumber,
            VotingEnabled = currentRound.VotingEnabled,
            MinLocationUpdateIntervalSeconds = session.GameTemplate.MinLocationUpdateIntervalSeconds,
            LocationBroadcastIntervalSeconds = session.GameTemplate.LocationUpdateIntervalSeconds
        };

        foreach (var team in session.Teams)
        {
            state.TeamScores[team.Id] = team.TotalScore;
        }

        foreach (var district in session.Districts)
        {
            state.DistrictOwnership[district.Id] = district.CurrentOwnerTeamSessionId;
        }

        foreach (var player in session.Players)
        {
            if (!teamSessionByTemplateId.TryGetValue(player.PlayerTemplate.TeamTemplateId, out var teamSessionId))
            {
                throw new InvalidOperationException(
                    $"Player session '{player.Id}' references team template '{player.PlayerTemplate.TeamTemplateId}', but no matching team session was found.");
            }

            state.PlayerTeamSessionMap[player.Id] = teamSessionId;
            state.PlayerLoggedIn[player.Id] = player.IsLoggedIn;
            state.PlayerRoles[player.Id] = player.PlayerTemplate.Role;
        }

        foreach (var round in orderedRounds)
        {
            state.OrderedRoundSessionIds.Add(round.Id);
            state.RoundNumbers[round.Id] = round.RoundTemplate.RoundNumber;
        }

        foreach (var persistedEvent in persistedEvents)
        {
            Apply(
                GameEventPayloadSerializer.Deserialize(persistedEvent.EventType, persistedEvent.Payload),
                state);
        }

        foreach (var player in session.Players)
        {
            state.PlayerLoggedIn[player.Id] = player.IsLoggedIn;
        }

        if (_state is not null && _state.GameSessionId != state.GameSessionId)
        {
            _locationMutexes.Clear();
        }

        _state = state;
        return state;
    }

    private sealed class AggregateState
    {
        public int GameSessionId { get; init; }

        public GamePhase CurrentPhase { get; set; }

        public int CurrentRoundSessionId { get; set; }

        public int CurrentRoundNumber { get; set; }

        public bool VotingEnabled { get; set; }

        public ConcurrentDictionary<int, int> TeamScores { get; } = [];

        public ConcurrentDictionary<int, int?> DistrictOwnership { get; } = [];

        public Dictionary<int, int> PlayerTeamSessionMap { get; } = [];

        public Dictionary<int, bool> PlayerLoggedIn { get; } = [];

        public Dictionary<int, PlayerRole> PlayerRoles { get; } = [];

        public Dictionary<int, DistrictClaimed> DistrictClaimsThisRound { get; } = [];

        public HashSet<int> TeamsThatVotedThisRound { get; } = [];

        public List<int> OrderedRoundSessionIds { get; } = [];

        public Dictionary<int, int> RoundNumbers { get; } = [];

        public Dictionary<int, DateTime> LastOpponentLocationBroadcastAt { get; } = [];

        public Dictionary<int, DateTime> LastLocationUpdateAt { get; } = [];

        public int MinLocationUpdateIntervalSeconds { get; init; }

        public int LocationBroadcastIntervalSeconds { get; init; }
    }
    
    public sealed record DevelopmentSessionResetResult(
        int GameSessionId,
        int CurrentRoundSessionId,
        GamePhase PreviousPhase,
        GamePhase CurrentPhase);
}
