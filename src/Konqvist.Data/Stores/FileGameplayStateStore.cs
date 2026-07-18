using System.IO;
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

        string json;
        try
        {
            json = File.ReadAllText(_filePath);
        }
        catch (IOException ex)
        {
            // Log the exception or handle it as appropriate
            Console.WriteLine($"Error reading game state file: {ex.Message}");
            return null; 
        }
        
        try
        {
            return JsonSerializer.Deserialize<GameplayState>(json, Options);
        }
        catch (JsonException ex)
        {
            // Log the exception or handle it as appropriate
            Console.WriteLine($"Error deserializing game state: {ex.Message}");
            return null;
        }
    }

    public void Write(GameplayState gameplayState)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        string json = JsonSerializer.Serialize(gameplayState, Options);
        
        // Use a temporary file for atomic writes
        string tempFilePath = _filePath + ".tmp";
        try
        {
            File.WriteAllText(tempFilePath, json);
            File.Move(tempFilePath, _filePath, true); // Overwrite the original file
        }
        catch (IOException ex)
        {
            // Log the exception or handle it as appropriate
            Console.WriteLine($"Error writing game state to file: {ex.Message}");
            // Clean up the temporary file if it exists
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
        catch (JsonException ex)
        {
            // Log the exception or handle it as appropriate
            Console.WriteLine($"Error serializing game state: {ex.Message}");
             // Clean up the temporary file if it exists
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    public void Clear()
    {
        if (File.Exists(_filePath))
            File.Delete(_filePath);
    }
}
