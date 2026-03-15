using System.Collections.Concurrent;

namespace Konqvist.Server.Hubs;

public interface IPlayerConnectionTracker
{
    bool AddConnection(int playerSessionId, string connectionId);

    bool RemoveConnection(int playerSessionId, string connectionId);

    bool HasConnections(int playerSessionId);
}

public sealed class PlayerConnectionTracker : IPlayerConnectionTracker
{
    private readonly ConcurrentDictionary<int, PlayerConnections> _connectionsByPlayer = [];

    public bool AddConnection(int playerSessionId, string connectionId)
    {
        var connections = _connectionsByPlayer.GetOrAdd(
            playerSessionId,
            _ => new PlayerConnections());

        lock (connections.SyncRoot)
        {
            var wasFirstConnection = connections.ConnectionIds.Count == 0;
            connections.ConnectionIds.Add(connectionId);
            return wasFirstConnection;
        }
    }

    public bool RemoveConnection(int playerSessionId, string connectionId)
    {
        if (!_connectionsByPlayer.TryGetValue(playerSessionId, out var connections))
        {
            return true;
        }

        lock (connections.SyncRoot)
        {
            connections.ConnectionIds.Remove(connectionId);
            if (connections.ConnectionIds.Count > 0)
            {
                return false;
            }

            _connectionsByPlayer.TryRemove(new KeyValuePair<int, PlayerConnections>(playerSessionId, connections));
            return true;
        }
    }

    public bool HasConnections(int playerSessionId)
    {
        if (!_connectionsByPlayer.TryGetValue(playerSessionId, out var connections))
        {
            return false;
        }

        lock (connections.SyncRoot)
        {
            return connections.ConnectionIds.Count > 0;
        }
    }

    private sealed class PlayerConnections
    {
        public object SyncRoot { get; } = new();

        public HashSet<string> ConnectionIds { get; } = new(StringComparer.Ordinal);
    }
}
