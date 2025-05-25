namespace Konqvist.Data.Models;

/// <summary>
///     Stores an amount of votes from a specific team for in a specific round.
/// </summary>
/// <param name="Voter">The team member who casts the vote</param>
/// <param name="Amount">The number of votes given</param>
/// <param name="Round">The round in which the vote was cast</param>
public record VoteData(string Voter, int Amount, int Round);