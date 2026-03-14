namespace Konqvist.Server.Domain.Events;

public sealed record VoteCast(
    int GameSessionId,
    int RoundSessionId,
    int VotingTeamSessionId,
    int TargetTeamSessionId,
    int ActorPlayerSessionId,
    int VoteValue,
    DateTime OccurredAt) : IGameDomainEvent
{
    public string EventType => nameof(VoteCast);

    int? IGameDomainEvent.RoundSessionId => RoundSessionId;

    int? IGameDomainEvent.ActorPlayerSessionId => ActorPlayerSessionId;
}
