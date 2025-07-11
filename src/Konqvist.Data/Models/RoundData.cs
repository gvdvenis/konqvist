namespace Konqvist.Data.Models;

public enum RoundKind
{
    NotStarted,
    GatherResources,
    Voting,
    GameOver
}

public record RoundData(int Index, string Title, RoundKind Kind, string? ResourceOfInterest, string Stakes)
{
    public static RoundData Empty { get; } = new(0, string.Empty, RoundKind.NotStarted, null, string.Empty);

    public static RoundData VoteRound(int index, string title, string resourceOfInterest, string stakes) =>
        new (index, title, RoundKind.Voting, resourceOfInterest, stakes);

    public static RoundData RunningRound(int index, string title, string resourceOfInterest) =>
        new(index, title, RoundKind.GatherResources, resourceOfInterest, string.Empty);

    public static RoundData WaitForStartRound(int index, string title) =>
        new(index, title, RoundKind.NotStarted, null, string.Empty);

    public static RoundData GameOverRound(int index, string title) =>
        new(index, title, RoundKind.GameOver, null, string.Empty);
} 