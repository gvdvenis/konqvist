namespace Konqvist.Admin.Features.Districts;

public sealed record RandomizeDistrictResourcesResult(
    RandomizeDistrictResourcesStatus Status,
    int DistrictsUpdated = 0)
{
    public static RandomizeDistrictResourcesResult Randomized(int districtsUpdated) =>
        new(RandomizeDistrictResourcesStatus.Randomized, districtsUpdated);

    public static RandomizeDistrictResourcesResult TemplateNotFound() =>
        new(RandomizeDistrictResourcesStatus.TemplateNotFound);

    public static RandomizeDistrictResourcesResult InvalidRange() =>
        new(RandomizeDistrictResourcesStatus.InvalidRange);
}
