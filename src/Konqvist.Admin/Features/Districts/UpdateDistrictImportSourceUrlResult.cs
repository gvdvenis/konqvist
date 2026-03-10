namespace Konqvist.Admin.Features.Districts;

public sealed record UpdateDistrictImportSourceUrlResult(
    DistrictImportSourceUrlStatus Status,
    string? SourceUrl = null,
    string? ErrorMessage = null)
{
    public static UpdateDistrictImportSourceUrlResult Updated(string sourceUrl) =>
        new(DistrictImportSourceUrlStatus.Updated, sourceUrl);

    public static UpdateDistrictImportSourceUrlResult Cleared() =>
        new(DistrictImportSourceUrlStatus.Cleared);

    public static UpdateDistrictImportSourceUrlResult TemplateNotFound() =>
        new(DistrictImportSourceUrlStatus.TemplateNotFound);

    public static UpdateDistrictImportSourceUrlResult InvalidSourceUrl(string? errorMessage = null) =>
        new(DistrictImportSourceUrlStatus.InvalidSourceUrl, null, errorMessage);
}
