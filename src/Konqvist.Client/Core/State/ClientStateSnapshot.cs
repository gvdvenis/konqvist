using Konqvist.Client.Core.Models;

namespace Konqvist.Client.Core.State;

public sealed record ClientStateSnapshot(
    GameStateSnapshot Game,
    MapStateSnapshot Map,
    VotingStateSnapshot Voting,
    ScoresStateSnapshot Scores,
    PlayerStateSnapshot Player);

public sealed record GameStateSnapshot(
    GamePhase CurrentPhase,
    int CurrentRoundNumber,
    int? GameSessionId);

public sealed record MapStateSnapshot(
    IReadOnlyDictionary<int, int?> DistrictOwners,
    IReadOnlyDictionary<int, RunnerPosition> RunnerPositions);

public sealed record VotingStateSnapshot(
    IReadOnlyDictionary<int, int> VotesPerTeam,
    bool IsVotingOpen,
    TimeSpan? TimeRemaining);

public sealed record ScoresStateSnapshot(
    IReadOnlyDictionary<int, int> TeamScores,
    IReadOnlyDictionary<int, TeamResourceTotals> TeamResources);

public sealed record PlayerStateSnapshot(
    int? PlayerSessionId,
    int? TeamSessionId,
    string? TeamName,
    PlayerRole? Role,
    bool IsLoggedIn,
    bool IsOnline);

public readonly record struct RunnerPosition(double Latitude, double Longitude);

public sealed record TeamResourceTotals(
    int Gold,
    int Voters,
    int Likes,
    int Oil);
