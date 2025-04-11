namespace Konqvist.Data.Models;

public enum RoundKind
{
    NotStarted,
    GatherResources,
    Voting,
    GameOver
}

public record RoundData(int Order, string Title, RoundKind Kind, string? ResourceOfInterest)
{
    public static RoundData Empty { get; } = new(0, string.Empty, RoundKind.NotStarted, null);
}