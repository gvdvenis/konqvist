namespace Konqvist.Client.Features.Voting.Store;

public sealed record VotingOpenedAction(TimeSpan? TimeRemaining);

public sealed record VoteCastAction(
    int VotingTeamSessionId,
    int TargetTeamSessionId,
    int VoteValue);

public sealed record VotingClosedAction;

public sealed record VotingTimerUpdatedAction(TimeSpan? TimeRemaining);
