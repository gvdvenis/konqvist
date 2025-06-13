namespace Konqvist.Data.Models;

/// <summary>
///     Stores a vote for a specific team in a specific round.
/// </summary>
/// <param name="Receiver">The team that received the vote</param>
/// <param name="Round">The round in which the vote was cast</param>
public record VoterData(string Receiver, int Round);