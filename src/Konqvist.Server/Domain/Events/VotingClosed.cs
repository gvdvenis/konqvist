namespace Konqvist.Server.Domain.Events;

public sealed record VotingClosed(
    int GameSessionId,
    int RoundSessionId,
    int? ActorPlayerSessionId,
    DateTime OccurredAt) : IGameDomainEvent
{
    public string EventType => nameof(VotingClosed);

    int? IGameDomainEvent.RoundSessionId => RoundSessionId;
}
