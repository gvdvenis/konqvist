namespace Konqvist.Data.Models;

public enum RoundKind
{
    NotStarted,
    GatherResources,
    Voting,
    Finished
}

public record RoundData(int Order, string Title, RoundKind Kind)
{
    public static RoundData Empty { get; } = new(0, string.Empty, RoundKind.NotStarted);
};