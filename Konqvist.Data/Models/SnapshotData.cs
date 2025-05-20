using Konqvist.Data.Contracts;
using Konqvist.Data.Stores;

namespace Konqvist.Data.Models;

internal record SnapshotData(
    RoundData Round,
    IEnumerable<DistrictOwner> DistrictOwners,
    IEnumerable<TeamResources> TeamResources,
    VotingData VotingData,
    IEnumerable<TeamScore> VotingScores,
    IEnumerable<TeamScore> TeamAdditionalResources
);
