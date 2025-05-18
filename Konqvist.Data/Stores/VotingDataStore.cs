using System.Collections.Concurrent;
using Konqvist.Data.Contracts;

namespace Konqvist.Data.Stores;

public class VotingDataStore
{
    // Dictionary<roundNumber, List<TeamVote>>
    private readonly ConcurrentDictionary<int, List<TeamVote>> _votesPerRound = new();
    // Dictionary<roundNumber, HashSet<voterTeamName>>
    private readonly ConcurrentDictionary<int, HashSet<string>> _teamsVotedPerRound = new();
    // Dictionary<roundNumber, List<Voter>>
    private readonly ConcurrentDictionary<int, List<Voter>> _votersPerRound = new();

    public void AddVote(int roundNumber, string recipientTeamName, string voterTeamName, int voteWeight)
    {
        var roundVotes = _votesPerRound.GetOrAdd(roundNumber, _ => []);
        var existingVote = roundVotes.FirstOrDefault(tv => tv.RecipientTeamName == recipientTeamName);
        if (existingVote != null)
        {
            roundVotes.Remove(existingVote);
            roundVotes.Add(new TeamVote(recipientTeamName, existingVote.VoteCount + voteWeight));
        }
        else
        {
            roundVotes.Add(new TeamVote(recipientTeamName, voteWeight));
        }

        var votedSet = _teamsVotedPerRound.GetOrAdd(roundNumber, _ => []);
        votedSet.Add(voterTeamName);

        var voters = _votersPerRound.GetOrAdd(roundNumber, _ => []);
        var existingVoter = voters.FirstOrDefault(v => v.VoterTeamName == voterTeamName);
        if (existingVoter != null)
        {
            voters.Remove(existingVoter);
        }
        voters.Add(new Voter(voterTeamName, recipientTeamName));
    }

    public int GetVotesForTeam(int roundNumber, string teamName)
    {
        if (!_votesPerRound.TryGetValue(roundNumber, out var roundVotes)) return 0;
        var vote = roundVotes.FirstOrDefault(tv => tv.RecipientTeamName == teamName);
        return vote?.VoteCount ?? 0;
    }

    public List<TeamVote> GetVotesForRound(int roundNumber)
    {
        return _votesPerRound.TryGetValue(roundNumber, out var roundVotes)
            ? [..roundVotes]
            : [];
    }

    public bool HasTeamVoted(int roundNumber, string teamName)
    {
        return _teamsVotedPerRound.TryGetValue(roundNumber, out var votedSet)
            && votedSet.Contains(teamName);
    }

    public List<Voter> GetVotersForRound(int roundNumber)
    {
        return _votersPerRound.TryGetValue(roundNumber, out var voters)
            ? [..voters]
            : [];
    }

    public IEnumerable<TeamVote> GetTeamVotesForRound(int roundNumber)
    {
        return GetVotesForRound(roundNumber);
    }

    public IEnumerable<Voter> GetTeamVotersForRound(int roundNumber)
    {
        return GetVotersForRound(roundNumber);
    }

    public void ClearVotesForRound(int roundNumber)
    {
        _votesPerRound.TryRemove(roundNumber, out _);
        _teamsVotedPerRound.TryRemove(roundNumber, out _);
        _votersPerRound.TryRemove(roundNumber, out _);
    }
}
