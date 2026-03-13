using System.Text.Json.Serialization;

namespace Konqvist.Client.Features.Login;

public sealed record LoginRequest([property: JsonPropertyName("token")] string Token);

public sealed record AuthErrorResponse([property: JsonPropertyName("message")] string Message);

public sealed record AuthIdentityResponse(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("team")] string Team,
    [property: JsonPropertyName("playerSessionId")] int PlayerSessionId);

public sealed record TeamStatusResponse(
    [property: JsonPropertyName("teamName")] string TeamName,
    [property: JsonPropertyName("runnerSlotTaken")] bool RunnerSlotTaken,
    [property: JsonPropertyName("runnerToken")] string RunnerToken,
    [property: JsonPropertyName("teamCaptainToken")] string TeamCaptainToken);

public sealed record LoginResult(bool IsSuccess, string? ErrorMessage)
{
    public static LoginResult Success() => new(true, null);
}

public sealed record LogoutResult(bool IsSuccess, string? ErrorMessage)
{
    public static LogoutResult Success() => new(true, null);
}

public sealed record TeamStatusResult(TeamStatusResponse? TeamStatus, string? ErrorMessage)
{
    public bool IsSuccess => TeamStatus is not null;
}
