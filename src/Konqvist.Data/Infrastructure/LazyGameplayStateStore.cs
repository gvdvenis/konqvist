using System.Diagnostics.CodeAnalysis;
using Konqvist.Data.Models;
using Konqvist.Data.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
    private readonly Lazy<SqlGameplayStateStore> _realStore;

    /// <summary>
    ///   Creates the wrapper. The <see cref="GameplayStateDbContext"/> is
    ///   resolved lazily (not captured eagerly) so the wrapper itself does not
    ///   trigger DbContext construction at DI-build time.
    /// </summary>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "DbContext is owned by DI and disposed by the container; SqlGameplayStateStore does not own it.")]
    public LazyGameplayStateStore(
        IServiceProvider services,
        string slot)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        if (string.IsNullOrWhiteSpace(slot))
            throw new ArgumentException("Persistence slot must be configured.", nameof(slot));
        _slot = slot;
        _realStore = new Lazy<SqlGameplayStateStore>(CreateRealStore, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public GameplayState? Read() => _realStore.Value.Read();

    public void Write(GameplayState gameplayState) => _realStore.Value.Write(gameplayState);

    public void Clear() => _realStore.Value.Clear();

    private SqlGameplayStateStore CreateRealStore()
    {
        // Resolve MapDataStore from the root provider so the singleton is reused
        // (this wrapper itself is a singleton). The DbContext is a scoped
        // service; resolve it from the root scope — EF Core registers the
        // context with a DI lifetime where resolving the singleton instance
        // directly is supported, matching the rest of the app's startup
        // pattern (e.g. the migration scope also resolves it this way).
        var db = _services.GetRequiredService<GameplayStateDbContext>();
        var map = _services.GetRequiredService<MapDataStore>();

        if (string.IsNullOrWhiteSpace(map.GameDefinitionHash))
        {
            throw new InvalidOperationException(
                "Cannot resolve the GameDefinitionId for gameplay-state persistence: " +
                "MapDataStore.GameDefinitionHash is empty. Ensure MapDataStore.InitializeAsync() " +
                "has run before any gameplay-state store operation.");
        }

        return new SqlGameplayStateStore(db, _slot, map.GameDefinitionHash);
    }
}
