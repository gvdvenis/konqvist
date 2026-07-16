using Konqvist.Data.Models;

namespace Konqvist.Data.Stores;

public interface IGameStateSnapshotStore
{
    GameStateSnapshot? Read();
    void Write(GameStateSnapshot snapshot);
    void Clear();
}

public sealed class InMemoryGameStateSnapshotStore : IGameStateSnapshotStore
{
    private GameStateSnapshot? _snapshot;

    public GameStateSnapshot? Read() => _snapshot;

    public void Write(GameStateSnapshot snapshot)
    {
        _snapshot = snapshot;
    }

    public void Clear()
    {
        _snapshot = null;
    }
}
