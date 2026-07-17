using Konqvist.Data.Models;

namespace Konqvist.Data.Stores;

public interface IGameplayStateStore
{
    GameplayState? Read();
    void Write(GameplayState gameplayState);
    void Clear();
}

public sealed class InMemoryGameplayStateStore : IGameplayStateStore
{
    private GameplayState? _gameplayState;

    public GameplayState? Read() => _gameplayState;

    public void Write(GameplayState gameplayState)
    {
        _gameplayState = gameplayState;
    }

    public void Clear()
    {
        _gameplayState = null;
    }
}
