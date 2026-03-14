using Konqvist.Infrastructure.Entities.Enums;

namespace Konqvist.Server.Domain.Events;

public sealed record RoundAdvanced(
    int GameSessionId,
    int PreviousRoundSessionId,
    int? NextRoundSessionId,
    int PreviousRoundNumber,
    int? NextRoundNumber,
    GamePhase PhaseAfterAdvance,
    int? ActorPlayerSessionId,
    DateTime OccurredAt) : IGameDomainEvent
{
    public string EventType => nameof(RoundAdvanced);

    int? IGameDomainEvent.RoundSessionId => PreviousRoundSessionId;
}
