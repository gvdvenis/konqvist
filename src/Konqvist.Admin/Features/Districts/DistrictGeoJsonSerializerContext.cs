using System.Text.Json.Serialization;

namespace Konqvist.Admin.Features.Districts;

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Serialization,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GeoJsonPolygon))]
internal partial class DistrictGeoJsonSerializerContext : JsonSerializerContext;
