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
    IGameEventWalWriter walWriter)
{
    private static readonly IReadOnlyDictionary<int, int> EmptyScores = new ConcurrentDictionary<int, int>();
    private static readonly IReadOnlyDictionary<int, int?> EmptyDistrictOwnership = new ConcurrentDictionary<int, int?>();

    private readonly SemaphoreSlim _mutex = new(1, 1);
    private AggregateState? _state;

    public GamePhase CurrentPhase => _state?.CurrentPhase ?? GamePhase.WaitingForPlayers;

    public int CurrentRoundNumber => _state?.CurrentRoundNumber ?? 0;

    public IReadOnlyDictionary<int, int> TeamScores => _state?.TeamScores ?? EmptyScores;

    public IReadOnlyDictionary<int, int?> DistrictOwnership => _state?.DistrictOwnership ?? EmptyDistrictOwnership;

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

            await walWriter.AppendAsync([gameEvent], cancellationToken);
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

            await walWriter.AppendAsync([gameEvent], cancellationToken);
            Apply(gameEvent, state);

            return new GameAggregateCommandResult<VoteCast>(gameEvent, [gameEvent]);
        }
        finally
        {
            _mutex.Release();
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

            await walWriter.AppendAsync(persistedEvents, cancellationToken);
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

            await walWriter.AppendAsync(persistedEvents, cancellationToken);
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

            await walWriter.AppendAsync(persistedEvents, cancellationToken);
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

            await walWriter.AppendAsync([gameEvent], cancellationToken);
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
            VotingEnabled = currentRound.VotingEnabled
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
    }
}
