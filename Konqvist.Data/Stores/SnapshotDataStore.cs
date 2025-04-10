using Konqvist.Data.Contracts;
using Konqvist.Data.Models;

namespace Konqvist.Data.Stores;

internal class SnapshotDataStore
{

    private readonly Dictionary<int, SnapshotData> _roundSnapshots = [];

    public void CreateSnapshot(MapData mapData, IEnumerable<TeamData> teamsData, int round)
    {
        var districtOwners = mapData.Districts
            .Where(d => d.Owner != null)
            .Select(d => new DistrictOwner(d.Owner!.Name, d.Name));

        var teamResources = teamsData.Select(t => new TeamResource(t, t.AdditionalResources));

        var newSnapshot = new SnapshotData(round, districtOwners, teamResources);

        _roundSnapshots[round] = newSnapshot;
    }
}
