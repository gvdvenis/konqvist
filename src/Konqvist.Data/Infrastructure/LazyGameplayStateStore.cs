using Konqvist.Data.Models;
using Konqvist.Data.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace Konqvist.Data.Infrastructure;

/// <summary>
///   Internal DI adapter (#19) that registers <see cref="SqlGameplayStateStore"/>
///   as the active <see cref="IGameplayStateStore"/> while breaking the
///   construction-time circular dependency between <see cref="MapDataStore"/>
///   and the SQL store.
///   <para>
///     <see cref="SqlGameplayStateStore"/> requires the <c>GameDefinitionId</c>,
///     which only <see cref="MapDataStore"/> can supply (via
///     <see cref="MapDataStore.GameDefinitionHash"/>). However,
///     <see cref="MapDataStore"/>'s constructor takes
///     <c>IGameplayStateStore?</c>, so DI building the SQL store eagerly to
///     satisfy <see cref="MapDataStore"/> would itself need
///     <see cref="MapDataStore"/> — a cycle.
///   </para>
///   <para>
///     This wrapper breaks the cycle by deferring the resolution of
///     <see cref="MapDataStore"/> and the construction of the real SQL store
///     to the first <see cref="Read"/>/<see cref="Write"/>/<see cref="Clear"/>
///     call. By that point the DI graph is fully constructed and
///     <see cref="MapDataStore"/> is available; <see cref="MapDataStore.InitializeAsync"/>
///     runs after construction and computes the hash before any persistence
///     call, so the composite key (Slot, GameDefinitionId) is correct on first
///     use. No change to <see cref="IGameplayStateStore"/>,
///     <see cref="MapDataStore"/>'s constructor contract, or the buffered
///     writer is required.
///   </para>
/// </summary>
internal sealed class LazyGameplayStateStore : IGameplayStateStore
{
    private readonly IServiceProvider _services;
    private readonly string _slot;
    private readonly Lazy<(string gameDefinitionId, IServiceScopeFactory scopeFactory)> _init;

    /// <summary>
    ///   Creates the wrapper. MapDataStore and the DbContext scope factory are
    ///   resolved lazily so the wrapper itself does not trigger DbContext
    ///   construction at DI-build time, and the scoped DbContext is never
    ///   captured in this singleton — a fresh scope is created per operation.
    /// </summary>
    public LazyGameplayStateStore(
        IServiceProvider services,
        string slot)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        if (string.IsNullOrWhiteSpace(slot))
            throw new ArgumentException("Persistence slot must be configured.", nameof(slot));
        _slot = slot;
        _init = new Lazy<(string, IServiceScopeFactory)>(ResolveKey, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public GameplayState? Read()
    {
        var (gameDefinitionId, scopeFactory) = _init.Value;
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameplayStateDbContext>();
        return new SqlGameplayStateStore(db, _slot, gameDefinitionId).Read();
    }

    public void Write(GameplayState gameplayState)
    {
        var (gameDefinitionId, scopeFactory) = _init.Value;
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameplayStateDbContext>();
        new SqlGameplayStateStore(db, _slot, gameDefinitionId).Write(gameplayState);
    }

    public void Clear()
    {
        var (gameDefinitionId, scopeFactory) = _init.Value;
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameplayStateDbContext>();
        new SqlGameplayStateStore(db, _slot, gameDefinitionId).Clear();
    }

    private (string gameDefinitionId, IServiceScopeFactory scopeFactory) ResolveKey()
    {
        var map = _services.GetRequiredService<MapDataStore>();
        var scopeFactory = _services.GetRequiredService<IServiceScopeFactory>();

        if (string.IsNullOrWhiteSpace(map.GameDefinitionHash))
        {
            throw new InvalidOperationException(
                "Cannot resolve the GameDefinitionId for gameplay-state persistence: " +
                "MapDataStore.GameDefinitionHash is empty. Ensure MapDataStore.InitializeAsync() " +
                "has run before any gameplay-state store operation.");
        }

        return (map.GameDefinitionHash, scopeFactory);
    }
}
