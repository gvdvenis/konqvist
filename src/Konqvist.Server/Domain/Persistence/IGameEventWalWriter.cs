using Konqvist.Server.Domain.Events;

namespace Konqvist.Server.Domain.Persistence;

public interface IGameEventWalWriter
{
    Task AppendAsync(IReadOnlyCollection<IGameDomainEvent> events, CancellationToken cancellationToken = default);
}
