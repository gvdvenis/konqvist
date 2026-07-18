using System.Text.Json;
using Konqvist.Data.Models;
using Konqvist.Data.Stores;
using Microsoft.EntityFrameworkCore;

namespace Konqvist.Data.Infrastructure;

/// <summary>
///   Azure SQL-backed implementation of <see cref="IGameplayStateStore"/>.
///   Persists one row per (Slot, GameDefinitionId) in the <c>GameplayStates</c>
///   table, storing the full <see cref="GameplayState"/> as validated JSON.
///   No Azure SQL concerns leak to gameplay-domain callers; the slot and
///   game-definition identity are supplied at construction (#19/#20 wires them).
/// </summary>
public sealed class SqlGameplayStateStore : IGameplayStateStore
{
    private readonly GameplayStateDbContext _db;
    private readonly string _slot;
    private readonly string _gameDefinitionId;

    public SqlGameplayStateStore(
        GameplayStateDbContext db,
        string slot,
        string gameDefinitionId)
    {
        _db = db;
        _slot = slot;
        _gameDefinitionId = gameDefinitionId;
    }

    public GameplayState? Read()
    {
        try
        {
            var row = _db.GameplayStates
                .AsNoTracking()
                .FirstOrDefault(e => e.Slot == _slot && e.GameDefinitionId == _gameDefinitionId);

            if (row is null)
                return null;

            return JsonSerializer.Deserialize<GameplayState>(row.Payload, GameplayStateJsonOptions.Instance);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading gameplay state from database: {ex.Message}");
            return null;
        }
    }

    public void Write(GameplayState gameplayState)
    {
        string payload = JsonSerializer.Serialize(gameplayState, GameplayStateJsonOptions.Instance);
        var nowUtc = DateTime.UtcNow;

        try
        {
            var existing = _db.GameplayStates
                .FirstOrDefault(e => e.Slot == _slot && e.GameDefinitionId == _gameDefinitionId);

            if (existing is not null)
            {
                existing.Payload = payload;
                existing.UpdatedAtUtc = nowUtc;
            }
            else
            {
                _db.GameplayStates.Add(new GameplayStateEntity
                {
                    Slot = _slot,
                    GameDefinitionId = _gameDefinitionId,
                    Payload = payload,
                    UpdatedAtUtc = nowUtc
                });
            }

            _db.SaveChanges();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing gameplay state to database: {ex.Message}");
        }
    }

    public void Clear()
    {
        try
        {
            var existing = _db.GameplayStates
                .FirstOrDefault(e => e.Slot == _slot && e.GameDefinitionId == _gameDefinitionId);

            if (existing is not null)
            {
                _db.GameplayStates.Remove(existing);
                _db.SaveChanges();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error clearing gameplay state in database: {ex.Message}");
        }
    }
}
