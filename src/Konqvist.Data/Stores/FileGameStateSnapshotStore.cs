using System.Text.Json;
using System.Text.Json.Serialization;
using Konqvist.Data;
using Konqvist.Data.Models;

namespace Konqvist.Data.Stores;

public sealed class FileGameStateSnapshotStore : IGameStateSnapshotStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new CoordinateConverter() }
    };

    private readonly string _filePath;

    public FileGameStateSnapshotStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Konqvist",
            "game-state.json");
    }

    public GameStateSnapshot? Read()
    {
        if (!File.Exists(_filePath))
            return null;

        string json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<GameStateSnapshot>(json, Options);
    }

    public void Write(GameStateSnapshot snapshot)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        string json = JsonSerializer.Serialize(snapshot, Options);
        File.WriteAllText(_filePath, json);
    }

    public void Clear()
    {
        if (File.Exists(_filePath))
            File.Delete(_filePath);
    }
}
