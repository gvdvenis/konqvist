using Konqvist.Data.Contracts;
using Konqvist.Data.Stores;
using System.Collections.Generic;

namespace Konqvist.Data.Models;

internal record SnapshotData(
    RoundData Round,
    IEnumerable<DistrictOwner> DistrictOwners,
    IEnumerable<TeamResources> TeamResources,
    VotingData VotingData,
    Dictionary<string, int> TeamScores
);
