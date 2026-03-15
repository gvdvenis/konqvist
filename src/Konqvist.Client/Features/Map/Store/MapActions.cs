namespace Konqvist.Client.Features.Map.Store;

public sealed record DistrictClaimedAction(
    int DistrictSessionId,
    int TeamSessionId);

public sealed record DistrictOwnershipChangedAction(
    int DistrictSessionId,
    int? TeamSessionId);

public sealed record LocationUpdatedAction(
    int PlayerSessionId,
    int TeamSessionId,
    double Latitude,
    double Longitude);
