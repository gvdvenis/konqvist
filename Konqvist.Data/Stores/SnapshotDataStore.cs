using Konqvist.Data.Contracts;
using Konqvist.Data.Models;

namespace Konqvist.Data.Stores;

public record TeamScore(string TeamName, int Score);

internal class SnapshotDataStore
{

    private readonly Dictionary<int, SnapshotData> _roundSnapshots = [];

    public void CreateSnapshot(MapData mapData, IEnumerable<TeamResources> teamResources, RoundData roundData)
    {
        var districtOwners = mapData.Districts
            .Where(d => d.Owner != null)
            .Select(d => new DistrictOwner(d.Owner!.Name, d.Name));

        var newSnapshot = new SnapshotData(roundData, districtOwners, teamResources);

        _roundSnapshots[roundData.Order] = newSnapshot;
    }

    internal void Clear()
    {
        _roundSnapshots.Clear();
    }

    public IEnumerable<TeamScore> GetAllTeamScores()
    {
        // Create a list of TeamScores that sums up all the scores for the relevant resources in all Voting rounds
        var votingRounds = _roundSnapshots.Values
            .Where(s => s.Round.Kind == RoundKind.Voting)
            .ToList();

        // Calculate total scores for each teams
        var totalTeamScores = votingRounds
            .SelectMany(vr => vr.TeamResources
                .Where(tr => !tr.Team.IsDisabled)
                .Select(tr => new TeamScore(tr.Team.Name, tr.GetScore())))
            .GroupBy(ts => ts.TeamName)
            .Select(group => new TeamScore(group.Key, group.Sum(ts => ts.Score)))
            .ToList();

        return totalTeamScores;
    }
}
