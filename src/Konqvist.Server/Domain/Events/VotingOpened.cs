namespace Konqvist.Server.Domain.Events;

public sealed record VotingOpened(
    int GameSessionId,
    int RoundSessionId,
    int? ActorPlayerSessionId,
    DateTime OccurredAt) : IGameDomainEvent
{
    public string EventType => nameof(VotingOpened);

    int? IGameDomainEvent.RoundSessionId => RoundSessionId;
}
