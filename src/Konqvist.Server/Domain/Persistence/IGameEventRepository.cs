using Konqvist.Server.Domain.Events;

namespace Konqvist.Server.Domain.Persistence;

public interface IGameEventRepository
{
    Task AppendAsync(IReadOnlyCollection<IGameDomainEvent> events, CancellationToken cancellationToken = default);
}
