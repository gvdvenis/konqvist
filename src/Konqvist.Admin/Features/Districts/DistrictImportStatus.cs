namespace Konqvist.Admin.Features.Districts;

public enum DistrictImportStatus
{
    Imported,
    TemplateNotFound,
    InvalidFileType,
    NoDistrictPolygonsFound,
    InvalidFileContent,
    SourceUrlNotConfigured,
    InvalidSourceUrl,
    SourceDownloadFailed,
    SourceContentEmpty
}
