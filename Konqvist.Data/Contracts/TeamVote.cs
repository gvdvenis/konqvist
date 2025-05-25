namespace Konqvist.Data.Contracts;

/// <summary>
///     Represents an amount of votes for a specific team.
/// </summary>
/// <param name="TeamName"></param>
/// <param name="Amount"></param>
public record TeamVote(string TeamName, int Amount);