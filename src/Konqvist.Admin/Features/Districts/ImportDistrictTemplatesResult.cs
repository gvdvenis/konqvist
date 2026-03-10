namespace Konqvist.Admin.Features.Districts;

public sealed record ImportDistrictTemplatesResult(
    DistrictImportStatus Status,
    DistrictImportSummary? Summary = null,
    string? ErrorMessage = null)
{
    public static ImportDistrictTemplatesResult Imported(DistrictImportSummary summary) =>
        new(DistrictImportStatus.Imported, summary);

    public static ImportDistrictTemplatesResult TemplateNotFound() =>
        new(DistrictImportStatus.TemplateNotFound);

    public static ImportDistrictTemplatesResult InvalidFileType() =>
        new(DistrictImportStatus.InvalidFileType);

    public static ImportDistrictTemplatesResult NoDistrictPolygonsFound() =>
        new(DistrictImportStatus.NoDistrictPolygonsFound);

    public static ImportDistrictTemplatesResult InvalidFileContent(string? errorMessage = null) =>
        new(DistrictImportStatus.InvalidFileContent, null, errorMessage);

    public static ImportDistrictTemplatesResult SourceUrlNotConfigured() =>
        new(DistrictImportStatus.SourceUrlNotConfigured);

    public static ImportDistrictTemplatesResult InvalidSourceUrl(string? errorMessage = null) =>
        new(DistrictImportStatus.InvalidSourceUrl, null, errorMessage);

    public static ImportDistrictTemplatesResult SourceDownloadFailed(string? errorMessage = null) =>
        new(DistrictImportStatus.SourceDownloadFailed, null, errorMessage);

    public static ImportDistrictTemplatesResult SourceContentEmpty(string? errorMessage = null) =>
        new(DistrictImportStatus.SourceContentEmpty, null, errorMessage);
}
