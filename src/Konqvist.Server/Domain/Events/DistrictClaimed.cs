namespace Konqvist.Server.Domain.Events;

public sealed record DistrictClaimed(
    int GameSessionId,
    int RoundSessionId,
    int DistrictSessionId,
    int TeamSessionId,
    int ActorPlayerSessionId,
    DateTime OccurredAt) : IGameDomainEvent
{
    public string EventType => nameof(DistrictClaimed);

    int? IGameDomainEvent.RoundSessionId => RoundSessionId;

    int? IGameDomainEvent.ActorPlayerSessionId => ActorPlayerSessionId;
}
