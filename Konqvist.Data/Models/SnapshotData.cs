using Konqvist.Data.Contracts;

namespace Konqvist.Data.Models;

internal record SnapshotData(int Round, IEnumerable<DistrictOwner> DistrictOwners, IEnumerable<TeamResource> Resources);
