using Konqvist.Data.Models;
using OpenLayers.Blazor;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Konqvist.Data;

public interface IMapDataLoader
{
    public Task<MapData> GetMapData();
    public Task<TeamData[]> GetTeamsData();
    public Task<List<RoundData>> GetRoundsData();
}

public class MapDataLoader: IMapDataLoader
{
    private static readonly string DataFolder =
        Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location)!, "Data");

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new CoordinateConverter(), new CoordinateArrayConverter() }
    };

    public async Task<MapData> GetMapData()
    {
        try
        {
            string str = await File.ReadAllTextAsync(Path.Combine(DataFolder, "map.json")).ConfigureAwait(false);

            var map = JsonSerializer.Deserialize<MapData>(str, Options);

            return map ?? MapData.Empty;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return MapData.Empty;
        }
    }

    public async Task<TeamData[]> GetTeamsData()
    {
        try
        {
            string str = await File.ReadAllTextAsync(Path.Combine(DataFolder, "teams.json"));

            var teams = JsonSerializer.Deserialize<TeamData[]>(str, Options);

            return teams ?? [];
        }
        catch (Exception)
        {
            return [];
        }
    }

    public Task<List<RoundData>> GetRoundsData() =>
        Task.FromResult<List<RoundData>>(
        [
            new RoundData(0, "Waiting for Game Start", RoundKind.NotStarted,null),
            new RoundData(1, "Running 1", RoundKind.GatherResources, nameof(ResourcesData.R1)),
            new RoundData(2, "Voting 1", RoundKind.Voting,nameof(ResourcesData.R1)),
            new RoundData(3, "Running 2", RoundKind.GatherResources, nameof(ResourcesData.R4)),
            new RoundData(4, "Voting 2", RoundKind.Voting, nameof(ResourcesData.R4)),
            new RoundData(5, "Running 3", RoundKind.GatherResources, nameof(ResourcesData.R2)),
            new RoundData(6, "Voting 3", RoundKind.Voting, nameof(ResourcesData.R2)),
            new RoundData(7, "Running 4", RoundKind.GatherResources, nameof(ResourcesData.R3)),
            new RoundData(8, "Voting 4", RoundKind.Voting, nameof(ResourcesData.R3)),
            new RoundData(9, "Game Over", RoundKind.GameOver, null)
        ]);
}

public class CoordinateConverter : JsonConverter<Coordinate>
{
    public override Coordinate Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected StartArray token.");

        // Read longitude
        reader.Read();
        var longitude = (float)reader.GetDouble();

        // Read latitude
        reader.Read();
        var latitude = (float)reader.GetDouble();

        // Read EndArray
        reader.Read();
        return new Coordinate(longitude, latitude);
    }

    public override void Write(Utf8JsonWriter writer, Coordinate value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.Longitude);
        writer.WriteNumberValue(value.Latitude);
        writer.WriteEndArray();
    }
}

public class CoordinateArrayConverter : JsonConverter<List<Coordinate>>
{
    public override List<Coordinate> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var coordinates = new List<Coordinate>();

        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected StartArray token.");

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                break;

            coordinates.Add(JsonSerializer.Deserialize<Coordinate>(ref reader, options));
        }

        return coordinates;
    }

    public override void Write(Utf8JsonWriter writer, List<Coordinate> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var coordinate in value)
        {
            JsonSerializer.Serialize(writer, coordinate, options);
        }

        writer.WriteEndArray();
    }
}