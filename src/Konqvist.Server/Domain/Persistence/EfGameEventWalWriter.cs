using Konqvist.Infrastructure.Entities.Session;
using Konqvist.Infrastructure.Persistence;
using Konqvist.Server.Domain.Events;
using Konqvist.Server.Domain.Serialization;
using Microsoft.EntityFrameworkCore;

namespace Konqvist.Server.Domain.Persistence;

public sealed class EfGameEventWalWriter(IDbContextFactory<KonqvistDbContext> dbContextFactory) : IGameEventWalWriter
{
    public async Task AppendAsync(IReadOnlyCollection<IGameDomainEvent> events, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (events.Count == 0)
        {
            return;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        foreach (var gameEvent in events)
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
