using System.Text.Json.Serialization;

namespace Konqvist.Client.Features.Game;

public sealed record GameErrorResponse([property: JsonPropertyName("message")] string Message);

public sealed record StartGameResult(bool IsSuccess, string? ErrorMessage)
{
    public static StartGameResult Success() => new(true, null);
}
