namespace Konqvist.Server.Domain.Events;

public sealed record LocationUpdated(
    int GameSessionId,
    int RoundSessionId,
    int PlayerSessionId,
    int TeamSessionId,
    double Latitude,
    double Longitude,
    bool Accepted,
    bool BroadcastToOpponents,
    DateTime OccurredAt) : IGameDomainEvent
{
    public string EventType => nameof(LocationUpdated);

    int? IGameDomainEvent.RoundSessionId => RoundSessionId;

    public int? ActorPlayerSessionId => PlayerSessionId;
}
