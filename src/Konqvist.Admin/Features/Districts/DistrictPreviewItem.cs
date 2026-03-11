namespace Konqvist.Admin.Features.Districts;

public sealed record DistrictPreviewItem(
    int Id,
    string Name,
    int Gold,
    int Voters,
    int Likes,
    int Oil,
    double TriggerLat,
    double TriggerLng,
    double? TriggerRadiusMeters,
    string GeoJson);
