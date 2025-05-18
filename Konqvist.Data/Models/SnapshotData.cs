using Konqvist.Data.Contracts;

namespace Konqvist.Data.Models;

internal record SnapshotData(
    RoundData Round,
    IEnumerable<DistrictOwner> DistrictOwners,
    IEnumerable<TeamResources> TeamResources,
    Dictionary<string, int> Votes,
    Dictionary<string, string> Voters);
