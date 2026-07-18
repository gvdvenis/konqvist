using System.Text.Json;
using System.Text.Json.Serialization;
using Konqvist.Data.Models;

namespace Konqvist.Data.Infrastructure;

/// <summary>
///   Shared <see cref="JsonSerializerOptions"/> for the gameplay-state payload.
///   Mirrors the options used by <c>FileGameplayStateStore</c> so JSON produced by
///   either store round-trips identically.
/// </summary>
internal static class GameplayStateJsonOptions
{
    public static readonly JsonSerializerOptions Instance = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new CoordinateConverter() }
    };
}
