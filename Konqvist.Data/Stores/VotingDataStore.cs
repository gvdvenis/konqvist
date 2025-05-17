using System.Collections.Concurrent;
using Konqvist.Data.Models;

namespace Konqvist.Data.Stores;

public class VotingDataStore
{
    // Dictionary<roundNumber, Dictionary<teamName, votes>>
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<string, int>> _votesPerRound = new();
    // Dictionary<roundNumber, HashSet<teamName>>
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<string, bool>> _teamsVotedPerRound = new();

    public void AddVote(int roundNumber, string teamName, int voteWeight)
    {
        var roundVotes = _votesPerRound.GetOrAdd(roundNumber, _ => new());
        roundVotes.AddOrUpdate(teamName, voteWeight, (_, old) => old + voteWeight);
        var votedSet = _teamsVotedPerRound.GetOrAdd(roundNumber, _ => new());
        votedSet[teamName] = true;
    }

    public int GetVotesForTeam(int roundNumber, string teamName)
    {
        if (_votesPerRound.TryGetValue(roundNumber, out var roundVotes) && roundVotes.TryGetValue(teamName, out var votes))
            return votes;
        return 0;
    }

    public Dictionary<string, int> GetVotesForRound(int roundNumber)
    {
        if (_votesPerRound.TryGetValue(roundNumber, out var roundVotes))
            return new Dictionary<string, int>(roundVotes);
        return new Dictionary<string, int>();
    }

    public bool HasTeamVoted(int roundNumber, string teamName)
    {
        if (_teamsVotedPerRound.TryGetValue(roundNumber, out var votedSet))
            return votedSet.ContainsKey(teamName);
        return false;
    }

    public void ClearVotesForRound(int roundNumber)
    {
        _votesPerRound.TryRemove(roundNumber, out _);
        _teamsVotedPerRound.TryRemove(roundNumber, out _);
    }

    public void ClearAll()
    {
        _votesPerRound.Clear();
        _teamsVotedPerRound.Clear();
    }
}
