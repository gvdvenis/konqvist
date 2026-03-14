namespace Konqvist.Server.Domain.Events;

public interface IGameDomainEvent
{
    string EventType { get; }

    int GameSessionId { get; }

    int? RoundSessionId { get; }

    int? ActorPlayerSessionId { get; }

    DateTime OccurredAt { get; }
}
