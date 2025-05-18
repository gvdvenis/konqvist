using System.Collections.Concurrent;

namespace Konqvist.Data.Stores;

public class VotingDataStore
{
    // Dictionary<roundNumber, Dictionary<teamName, votes>>
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<string, int>> _votesPerRound = new();
    // Dictionary<roundNumber, HashSet<voterTeamName>>
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<string, bool>> _teamsVotedPerRound = new();
    // Dictionary<roundNumber, Dictionary<voterTeamName, recipientTeamName>>
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<string, string>> _votersPerRound = new();

    public void AddVote(int roundNumber, string recipientTeamName, string voterTeamName, int voteWeight)
    {
        var roundVotes = _votesPerRound.GetOrAdd(roundNumber, _ => new ConcurrentDictionary<string, int>());
        roundVotes.AddOrUpdate(recipientTeamName, voteWeight, (_, old) => old + voteWeight);

        var votedSet = _teamsVotedPerRound.GetOrAdd(roundNumber, _ => new ConcurrentDictionary<string, bool>());
        votedSet[voterTeamName] = true;

        var voters = _votersPerRound.GetOrAdd(roundNumber, _ => new ConcurrentDictionary<string, string>());
        voters[voterTeamName] = recipientTeamName;
    }

    public int GetVotesForTeam(int roundNumber, string teamName)
    {
        if (_votesPerRound.TryGetValue(roundNumber, out var roundVotes) && roundVotes.TryGetValue(teamName, out int votes))
            return votes;
        return 0;
    }

    public Dictionary<string, int> GetVotesForRound(int roundNumber)
    {
        return _votesPerRound.TryGetValue(roundNumber, out var roundVotes) 
            ? new (roundVotes) 
            : new ();
    }

    public bool HasTeamVoted(int roundNumber, string teamName)
    {
        return _teamsVotedPerRound.TryGetValue(
            roundNumber, 
            out var votedSet) 
               && votedSet.ContainsKey(teamName);
    }

    public Dictionary<string, string> GetVotersForRound(int roundNumber)
    {
        return _votersPerRound.TryGetValue(roundNumber, out var voters)
            ? new(voters)
            : new();
    }

    public void ClearVotesForRound(int roundNumber)
    {
        _votesPerRound.TryRemove(roundNumber, out _);
        _teamsVotedPerRound.TryRemove(roundNumber, out _);
        _votersPerRound.TryRemove(roundNumber, out _);
    }
}
