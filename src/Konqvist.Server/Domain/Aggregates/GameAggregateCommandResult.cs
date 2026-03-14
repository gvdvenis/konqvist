using Konqvist.Server.Domain.Events;

namespace Konqvist.Server.Domain.Aggregates;

public sealed record GameAggregateCommandResult<TEvent>(
    TEvent PrimaryEvent,
    IReadOnlyList<IGameDomainEvent> PersistedEvents,
    bool WasIdempotent = false)
    where TEvent : IGameDomainEvent;
