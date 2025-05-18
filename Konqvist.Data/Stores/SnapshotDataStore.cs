using Konqvist.Data.Contracts;
using Konqvist.Data.Models;

namespace Konqvist.Data.Stores;

public record TeamScore(string TeamName, int Score);

/// <summary>
/// Stores round snapshots and calculates team scores including voting bonuses.
/// </summary>
internal class SnapshotDataStore
{
    private const int BonusTotal = 150;
    private readonly Dictionary<int, SnapshotData> _roundSnapshots = [];

    public void CreateSnapshot(MapData mapData, IEnumerable<TeamResources> teamResources, RoundData roundData, Dictionary<string, int> votes, Dictionary<string, string> voters)
    {
        var districtOwners = mapData.Districts
            .Where(d => d.Owner != null)
            .Select(d => new DistrictOwner(d.Owner!.Name, d.Name));

        var newSnapshot = new SnapshotData(roundData, districtOwners, teamResources, votes, voters);
        _roundSnapshots[roundData.Order] = newSnapshot;
    }

    internal void Clear()
    {
        _roundSnapshots.Clear();
    }

    /// <summary>
    /// Calculates the total team scores for all voting rounds, including voting bonus points for the last completed round.
    /// </summary>
    public IEnumerable<TeamScore> GetAllTeamScores()
    {
        // Get all voting rounds in order
        var votingRounds = _roundSnapshots.Values
            .Where(s => s.Round.Kind == RoundKind.Voting)
            .OrderBy(s => s.Round.Order)
            .ToList();

        // Calculate base scores for each team
        var baseScores = votingRounds
            .SelectMany(vr => vr.TeamResources
                .Where(tr => !tr.Team.IsDisabled)
                .Select(tr => new TeamScore(tr.Team.Name, tr.GetScore())))
            .GroupBy(ts => ts.TeamName)
            .ToDictionary(g => g.Key, g => g.Sum(ts => ts.Score));

        // Calculate bonus points for the last completed voting round (if any)
        int lastVotingRoundOrder = votingRounds.Select(vr => vr.Round.Order).DefaultIfEmpty(-1).Max();
        var lastVotingRound = votingRounds.FirstOrDefault(vr => vr.Round.Order == lastVotingRoundOrder);
        var bonusPoints = new Dictionary<string, int>();
        if (lastVotingRound is { Votes.Count: > 0, Voters.Count: > 0 })
        {
            int maxVotes = lastVotingRound.Votes.Values.Max();
            var winningTeams = lastVotingRound.Votes.Where(kvp => kvp.Value == maxVotes).Select(kvp => kvp.Key).ToList();
            var votersForWinners = lastVotingRound.Voters.Where(kvp => winningTeams.Contains(kvp.Value)).Select(kvp => kvp.Key).ToList();
            int bonusPerTeam = votersForWinners.Count > 0 ? BonusTotal / votersForWinners.Count : 0;
            foreach (string teamName in votersForWinners)
                bonusPoints[teamName] = bonusPerTeam;
        }

        // Combine base scores and bonus points
        var allTeamNames = baseScores.Keys.Union(bonusPoints.Keys);
        return allTeamNames.Select(teamName =>
            new TeamScore(teamName, baseScores.GetValueOrDefault(teamName, 0) + bonusPoints.GetValueOrDefault(teamName, 0))
        ).ToList();
    }
}
