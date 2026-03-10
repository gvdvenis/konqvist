namespace Konqvist.Admin.Features.Districts;

public sealed record DistrictImportSummary(
    int DistrictsImported,
    int TriggerCirclesMatched,
    int TriggerCentersDerived);
