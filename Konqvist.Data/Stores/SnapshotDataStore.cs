using Konqvist.Data.Contracts;
using Konqvist.Data.Models;

namespace Konqvist.Data.Stores;

public record TeamScore(string TeamName, int Score)
{
    public static TeamScore operator +(TeamScore a, TeamScore b)
    {
        if (a.TeamName != b.TeamName)
            throw new InvalidOperationException("Cannot add TeamScores for different teams.");
        return a with { Score = a.Score + b.Score };
    }
}

/// <summary>
/// Stores round snapshots and calculates team scores including voting bonuses.
/// </summary>
internal class SnapshotDataStore
{
    private readonly Dictionary<int, SnapshotData> _roundSnapshots = [];

    public void CreateSnapshot(MapData mapData,
        List<TeamResources> teamResources,
        RoundData roundData,
        VotingData votingData)
    {
        var districtOwners = mapData.Districts
            .Where(d => d.Owner != null)
            .Select(d => new DistrictOwner(d.Owner!.Name, d.Name));

        // Calculate VoteWeight for each team
        var voteWeights = teamResources.ToDictionary(
            tr => tr.Team.Name,
            tr => tr.AdditionalResources.CalculateVoteWeight(roundData.ResourceOfInterest)
                + tr.DistrictResources.CalculateVoteWeight(roundData.ResourceOfInterest)
        );

        // Calculate VoteBonus for this round using VotingData
        var voteBonuses = votingData
            .GetVotingBonuses()
            .ToDictionary(ts => ts.TeamName, ts => ts);

        // Calculate and store running totals for each team for this round
        var teamScores = new Dictionary<string, TeamScore>();
        foreach (var tr in teamResources)
        {
            var teamName = tr.Team.Name;
            TeamScore prevScore = new TeamScore(teamName, 0);
            // Find previous round's score if any
            var prevSnapshot = _roundSnapshots.OrderBy(kvp => kvp.Key).LastOrDefault(s => s.Key < roundData.Index).Value;
            if (prevSnapshot != null && prevSnapshot.TeamScores.TryGetValue(teamName, out var prev))
                prevScore = new TeamScore(teamName, prev);
            int voteWeight = voteWeights.GetValueOrDefault(teamName, 0);
            var bonus = voteBonuses.GetValueOrDefault(teamName, new TeamScore(teamName, 0));
            var newTotal = prevScore + new TeamScore(teamName, voteWeight) + bonus;
            teamScores[teamName] = newTotal;
        }

        var newSnapshot = new SnapshotData(roundData, districtOwners, teamResources, votingData, teamScores.ToDictionary(x => x.Key, x => x.Value.Score));
        _roundSnapshots[roundData.Index] = newSnapshot;
    }

    internal void Clear()
    {
        _roundSnapshots.Clear();
    }

    /// <summary>
    ///     Calculates the total team scores for all voting rounds, including voting bonus points for the last completed round.
    /// </summary>
    public List<TeamScore> GetAllTeamScores()
    {
        // Get the latest snapshot for each team and return its score
        if (_roundSnapshots.Count == 0)
            return [];

        var lastSnapshot = _roundSnapshots.Last().Value;
        return lastSnapshot.TeamScores.Select(kvp => new TeamScore(kvp.Key, kvp.Value)).ToList();
    }

}
