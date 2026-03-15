using System.Text.Json.Serialization;
using Konqvist.Infrastructure.Entities.Enums;

namespace Konqvist.Server.Features.SessionState;

public sealed record SessionStateResponse(
    [property: JsonPropertyName("Game")] SessionGameStateSnapshot Game,
    [property: JsonPropertyName("Map")] SessionMapStateSnapshot Map,
    [property: JsonPropertyName("Voting")] SessionVotingStateSnapshot Voting,
    [property: JsonPropertyName("Scores")] SessionScoresStateSnapshot Scores,
    [property: JsonPropertyName("Player")] SessionPlayerStateSnapshot Player);

public sealed record SessionGameStateSnapshot(
    [property: JsonPropertyName("CurrentPhase")] GamePhase CurrentPhase,
    [property: JsonPropertyName("CurrentRoundNumber")] int CurrentRoundNumber,
    [property: JsonPropertyName("GameSessionId")] int? GameSessionId);

public sealed record SessionMapStateSnapshot(
    [property: JsonPropertyName("DistrictOwners")] IReadOnlyDictionary<int, int?> DistrictOwners,
    [property: JsonPropertyName("RunnerPositions")] IReadOnlyDictionary<int, SessionRunnerPosition> RunnerPositions);

public sealed record SessionVotingStateSnapshot(
    [property: JsonPropertyName("VotesPerTeam")] IReadOnlyDictionary<int, int> VotesPerTeam,
    [property: JsonPropertyName("IsVotingOpen")] bool IsVotingOpen,
    [property: JsonPropertyName("TimeRemaining")] TimeSpan? TimeRemaining);

public sealed record SessionScoresStateSnapshot(
    [property: JsonPropertyName("TeamScores")] IReadOnlyDictionary<int, int> TeamScores,
    [property: JsonPropertyName("TeamResources")] IReadOnlyDictionary<int, SessionTeamResourceTotals> TeamResources);

public sealed record SessionPlayerStateSnapshot(
    [property: JsonPropertyName("PlayerSessionId")] int? PlayerSessionId,
    [property: JsonPropertyName("TeamSessionId")] int? TeamSessionId,
    [property: JsonPropertyName("TeamName")] string? TeamName,
    [property: JsonPropertyName("Role")] PlayerRole? Role,
    [property: JsonPropertyName("IsLoggedIn")] bool IsLoggedIn,
    [property: JsonPropertyName("IsOnline")] bool IsOnline);

public readonly record struct SessionRunnerPosition(
    [property: JsonPropertyName("Latitude")] double Latitude,
    [property: JsonPropertyName("Longitude")] double Longitude);

public sealed record SessionTeamResourceTotals(
    [property: JsonPropertyName("Gold")] int Gold,
    [property: JsonPropertyName("Voters")] int Voters,
    [property: JsonPropertyName("Likes")] int Likes,
    [property: JsonPropertyName("Oil")] int Oil);
