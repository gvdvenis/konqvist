using Konqvist.Infrastructure.Entities.Session;
using Konqvist.Infrastructure.Persistence;
using Konqvist.Server.Domain.Events;
using Konqvist.Server.Domain.Serialization;
using Microsoft.EntityFrameworkCore;

namespace Konqvist.Server.Domain.Persistence;

public sealed class GameEventRepository(IDbContextFactory<KonqvistDbContext> dbContextFactory) : IGameEventRepository
{
    private static readonly HashSet<string> PersistedEventTypes =
    [
        nameof(DistrictClaimed),
        nameof(VoteCast),
        nameof(VotingOpened),
        nameof(VotingClosed),
        nameof(RoundAdvanced),
        nameof(RunnerLogin),
        nameof(RunnerLogout),
        nameof(GamePhaseChanged)
    ];

    public async Task AppendAsync(IReadOnlyCollection<IGameDomainEvent> events, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (events.Count == 0)
        {
            return;
        }

        var persistedEvents = events
            .Where(gameEvent => PersistedEventTypes.Contains(gameEvent.EventType))
            .ToList();

        if (persistedEvents.Count == 0)
        {
            return;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        foreach (var gameEvent in persistedEvents)
        {
            dbContext.GameEvents.Add(new GameEvent
            {
                GameSessionId = gameEvent.GameSessionId,
                RoundSessionId = gameEvent.RoundSessionId,
                EventType = gameEvent.EventType,
                Payload = GameEventPayloadSerializer.Serialize(gameEvent),
                OccurredAt = gameEvent.OccurredAt,
                ActorPlayerSessionId = gameEvent.ActorPlayerSessionId
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
