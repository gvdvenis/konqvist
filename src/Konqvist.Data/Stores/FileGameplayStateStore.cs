using System.Text.Json;
using System.Text.Json.Serialization;
using Konqvist.Data;
using Konqvist.Data.Models;

namespace Konqvist.Data.Stores;

public sealed class FileGameplayStateStore : IGameplayStateStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new CoordinateConverter() }
    };

    private readonly string _filePath;

    public FileGameplayStateStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Konqvist",
            "game-state.json");
    }

    public GameplayState? Read()
    {
        if (!File.Exists(_filePath))
            return null;

        string json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<GameplayState>(json, Options);
    }

    public void Write(GameplayState gameplayState)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        string json = JsonSerializer.Serialize(gameplayState, Options);
        File.WriteAllText(_filePath, json);
    }

    public void Clear()
    {
        if (File.Exists(_filePath))
            File.Delete(_filePath);
    }
}
