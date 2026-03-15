namespace Konqvist.Server.Hubs;

public sealed record GameStartedMessage(
    int GameSessionId,
    int RoundNumber,
    string Phase,
    DateTime OccurredAt);

public sealed record DistrictClaimedMessage(
    int GameSessionId,
    int RoundSessionId,
    int DistrictSessionId,
    int TeamSessionId,
    int ActorPlayerSessionId,
    DateTime OccurredAt);

public sealed record DistrictOwnershipChangedMessage(
    int GameSessionId,
    int RoundSessionId,
    int DistrictSessionId,
    int? PreviousTeamSessionId,
    int? CurrentTeamSessionId,
    int ActorPlayerSessionId,
    DateTime OccurredAt);

public sealed record PhaseChangedMessage(
    int GameSessionId,
    int? RoundSessionId,
    string PreviousPhase,
    string CurrentPhase,
    int? ActorPlayerSessionId,
    DateTime OccurredAt);

public sealed record VoteStartedMessage(
    int GameSessionId,
    int RoundSessionId,
    int? ActorPlayerSessionId,
    DateTime OccurredAt);

public sealed record VoteCastMessage(
    int GameSessionId,
    int RoundSessionId,
    int VotingTeamSessionId,
    int TargetTeamSessionId,
    int ActorPlayerSessionId,
    int VoteValue,
    DateTime OccurredAt);

public sealed record VoteEndedMessage(
    int GameSessionId,
    int RoundSessionId,
    int? ActorPlayerSessionId,
    DateTime OccurredAt);

public sealed record ScoreUpdatedMessage(
    int GameSessionId,
    int TeamSessionId,
    int TotalScore,
    int TotalGold,
    int TotalVoters,
    int TotalLikes,
    int TotalOil,
    DateTime OccurredAt);

public sealed record GameStateChangedMessage(
    int GameSessionId,
    int? RoundSessionId,
    string CurrentPhase,
    int CurrentRoundNumber,
    DateTime OccurredAt);

public sealed record RoundEndedMessage(
    int GameSessionId,
    int RoundSessionId,
    int RoundNumber,
    string CurrentPhase,
    DateTime OccurredAt);

public sealed record RunnerLoggedOutMessage(
    int GameSessionId,
    int TargetPlayerSessionId,
    int? ActorPlayerSessionId,
    DateTime OccurredAt);

public sealed record LocationUpdatedMessage(
    int GameSessionId,
    int RoundSessionId,
    int PlayerSessionId,
    int TeamSessionId,
    double Latitude,
    double Longitude,
    DateTime OccurredAt);

public sealed record RunnerStateChangedMessage(
    int GameSessionId,
    int PlayerSessionId,
    int TeamSessionId,
    bool IsLoggedIn,
    bool IsOnline,
    DateTime? LastSeen,
    DateTime OccurredAt);
