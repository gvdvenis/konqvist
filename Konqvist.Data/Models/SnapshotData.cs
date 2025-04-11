using Konqvist.Data.Contracts;

namespace Konqvist.Data.Models;

internal record SnapshotData(
    RoundData Round,
    IEnumerable<DistrictOwner> DistrictOwners,
    IEnumerable<TeamResources> TeamResources);
