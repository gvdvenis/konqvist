using Konqvist.Infrastructure.Entities.Enums;

namespace Konqvist.Server.Domain.Events;

public sealed record GamePhaseChanged(
    int GameSessionId,
    int? RoundSessionId,
    GamePhase PreviousPhase,
    GamePhase CurrentPhase,
    int? ActorPlayerSessionId,
    DateTime OccurredAt) : IGameDomainEvent
{
    public string EventType => nameof(GamePhaseChanged);
}
