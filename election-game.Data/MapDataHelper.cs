using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using election_game.Data.Model.MapElements;
using OpenLayers.Blazor;

namespace election_game.Data;

public static class MapDataHelper
{

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new CoordinateConverter(), new CoordinateArrayConverter() }
    };

    public static async Task<MapData?> GetMapData()
    {
        string rootPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        string str = await File.ReadAllTextAsync(Path.Combine(rootPath, "Data","map.json"));
        
        var map = JsonSerializer.Deserialize<MapData>(str, Options);
        
        return map;
    }
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