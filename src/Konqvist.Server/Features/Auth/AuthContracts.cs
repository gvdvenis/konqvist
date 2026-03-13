using System.Text.Json.Serialization;

namespace Konqvist.Server.Features.Auth;

public sealed record LoginRequest([property: JsonPropertyName("token")] string Token);

public sealed record AuthIdentityResponse(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("team")] string Team,
    [property: JsonPropertyName("playerSessionId")] int PlayerSessionId);

public sealed record AuthErrorResponse([property: JsonPropertyName("message")] string Message);
