using System.Text.Json.Serialization;

namespace Konqvist.Server.Features.Game;

public sealed record GameErrorResponse([property: JsonPropertyName("message")] string Message);
