using Konqvist.Data.Contracts;
using Konqvist.Data.Models;

namespace Konqvist.Data.Stores;

public record TeamScore(string TeamName, int Score)
{
    public static TeamScore operator +(TeamScore a, TeamScore b)
    {
        if (a.TeamName != b.TeamName)
            throw new InvalidOperationException("Cannot add VotingScores for different teams.");
        return a with { Score = a.Score + b.Score };
    }
}

/// <summary>
/// Stores round snapshots and calculates team scores including voting bonuses.
/// </summary>
public class SnapshotDataStore
{
    private readonly Dictionary<int, SnapshotData> _roundSnapshots = [];

    public void CreateSnapshot(MapData mapData,
        List<TeamResources> teamResources,
        RoundData roundData,
        VotingData votingData)
    {
        var districtOwners = GetDistrictOwners(mapData);
        var voteBonuses = CalculateVoteBonuses(votingData);
        var teamAdditionalResources = GetTeamAdditionalResources(teamResources);
        var teamScores = CalculateTeamScores(teamResources, roundData, voteBonuses, teamAdditionalResources);

        var newSnapshot = new SnapshotData(
            roundData,
            districtOwners,
            teamResources,
            votingData,
            teamScores,
            teamAdditionalResources
        );

        _roundSnapshots[roundData.Index] = newSnapshot;
    }

    private static List<DistrictOwner> GetDistrictOwners(MapData mapData)
    {
        return mapData.Districts
            .Where(d => d.Owner != null)
            .Select(d => new DistrictOwner(d.Owner!.Name, d.Name))
            .ToList();
    }

    private static List<TeamScore> GetTeamAdditionalResources(List<TeamResources> teamResources)
    {
        return teamResources
            .Select(tr => new TeamScore(tr.Team.Name, tr.AdditionalResources.Total))
            .ToList();
    }

    private static List<TeamScore> CalculateVoteBonuses(VotingData votingData)
    {
        return votingData
            .GetVotingBonuses()
            .ToList();
    }

    private List<TeamScore> CalculateTeamScores(
        List<TeamResources> teamResources,
        RoundData roundData,
        List<TeamScore> voteBonuses,
        List<TeamScore> teamAdditionalResources)
    {
        return teamResources
            .Select(tr =>
            {
                string teamName = tr.Team.Name;
                int previousScore = GetPreviousScore(teamName, roundData.Index);
                int additional = teamAdditionalResources.GetScoreForTeam(teamName);
                int bonus = voteBonuses.GetScoreForTeam(teamName);
                return new TeamScore(teamName, previousScore + additional + bonus);
            })
            .ToList();
    }

    private int GetPreviousScore(string teamName, int currentRoundIndex)
    {
        var prevSnapshot = _roundSnapshots.OrderBy(kvp => kvp.Key).LastOrDefault(s => s.Key < currentRoundIndex).Value;
        return prevSnapshot?.VotingScores.GetScoreForTeam(teamName) ?? 0;
    }

    public void Clear()
    {
        _roundSnapshots.Clear();
    }

    /// <summary>
    ///     Calculates the total team scores for all voting rounds, including voting bonus points for the last completed round.
    /// </summary>
    public List<TeamScore> GetAllTeamScores()
    {
        if (_roundSnapshots.Count == 0)
            return [];

        var lastSnapshot = _roundSnapshots.Last().Value;
        return lastSnapshot.VotingScores.ToList();
    }
}
