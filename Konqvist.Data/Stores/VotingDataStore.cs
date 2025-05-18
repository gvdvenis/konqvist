using System.Collections.Concurrent;
using Konqvist.Data.Contracts;

namespace Konqvist.Data.Stores;

public record VotingData(List<TeamVote> Votes, List<Voter> Voters)
{
    /// <summary>
    ///     Calculates the voting bonus for each voter who voted for the team(s) with the highest votes.
    /// </summary>
    /// <param name="bonusTotal">Total bonus points to split</param>
    /// <returns>List of TeamScore for each voter team</returns>
    public List<TeamScore> GetVotingBonuses(int bonusTotal = 150)
    {
        if (Votes.Count == 0 || Voters.Count == 0)
            return [];

        int maxVotes = Votes.Max(v => v.VoteCount);

        var winningTeams = Votes
            .Where(v => v.VoteCount == maxVotes)
            .Select(v => v.RecipientTeamName)
            .ToHashSet();

        var votersForWinners = Voters
            .Where(v => winningTeams.Contains(v.RecipientTeamName))
            .Select(v => v.VoterTeamName)
            .Distinct()
            .ToList();

        int bonusPerVoter = votersForWinners.Count > 0 
            ? bonusTotal / votersForWinners.Count 
            : 0;

        return votersForWinners.Select(teamName => new TeamScore(teamName, bonusPerVoter)).ToList();
    }
}

public class VotingDataStore
{
    // Dictionary<roundNumber, VotingData>
    private readonly ConcurrentDictionary<int, VotingData> _votingDataPerRound = new();

    public void AddVote(int roundNumber, string recipientTeamName, string voterTeamName, int voteWeight)
    {
        // Get or create the voting data for this round
        var (teamVotes, voters) = _votingDataPerRound.GetOrAdd(roundNumber, _ => new VotingData([], []));

        // Update the vote count for the recipient team
        UpdateTeamVotes(teamVotes, recipientTeamName, voteWeight);

        // Register the voter for this round
        RegisterVoter(voters, voterTeamName, recipientTeamName);
    }

    private static void UpdateTeamVotes(List<TeamVote> teamVotes, string recipientTeamName, int voteWeight)
    {
        var existingVote = teamVotes.FirstOrDefault(tv => tv.RecipientTeamName == recipientTeamName);
        if (existingVote != null)
        {
            teamVotes.Remove(existingVote);
            teamVotes.Add(new TeamVote(recipientTeamName, existingVote.VoteCount + voteWeight));
        }
        else
        {
            teamVotes.Add(new TeamVote(recipientTeamName, voteWeight));
        }
    }

    private static void RegisterVoter(List<Voter> voters, string voterTeamName, string recipientTeamName)
    {
        var existingVoter = voters.FirstOrDefault(v => v.VoterTeamName == voterTeamName);
        if (existingVoter != null)
        {
            voters.Remove(existingVoter);
        }
        voters.Add(new Voter(voterTeamName, recipientTeamName));
    }

    public int GetVotesForTeam(int roundNumber, string teamName)
    {
        if (!_votingDataPerRound.TryGetValue(roundNumber, out var votingData)) return 0;
        var vote = votingData.Votes.FirstOrDefault(tv => tv.RecipientTeamName == teamName);
        return vote?.VoteCount ?? 0;
    }

    public List<TeamVote> GetVotesForRound(int roundNumber)
    {
        return _votingDataPerRound.TryGetValue(roundNumber, out var votingData)
            ? [..votingData.Votes]
            : [];
    }

    public bool HasTeamVoted(int roundNumber, string teamName)
    {
        return _votingDataPerRound.TryGetValue(roundNumber, out var votingData) && 
               votingData.Voters.Any(v => v.VoterTeamName == teamName);
    }

    public List<Voter> GetVotersForRound(int roundNumber)
    {
        return _votingDataPerRound.TryGetValue(roundNumber, out var votingData)
            ? [..votingData.Voters]
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
}
