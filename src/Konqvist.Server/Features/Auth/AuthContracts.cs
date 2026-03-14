using System.Text.Json.Serialization;

namespace Konqvist.Server.Features.Auth;

public sealed record LoginRequest([property: JsonPropertyName("token")] string Token);

public sealed record AuthIdentityResponse(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("team")] string Team,
    [property: JsonPropertyName("playerSessionId")] int PlayerSessionId,
    [property: JsonPropertyName("gameStatus")] string GameStatus,
    [property: JsonPropertyName("gamePhase")] string GamePhase);

public sealed record AuthErrorResponse([property: JsonPropertyName("message")] string Message);

public sealed record TeamStatusResponse(
    [property: JsonPropertyName("teamName")] string TeamName,
    [property: JsonPropertyName("runnerSlotTaken")] bool RunnerSlotTaken,
    [property: JsonPropertyName("runnerToken")] string RunnerToken,
    [property: JsonPropertyName("teamCaptainToken")] string TeamCaptainToken);
