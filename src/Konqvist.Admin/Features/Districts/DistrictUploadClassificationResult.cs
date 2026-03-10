namespace Konqvist.Admin.Features.Districts;

public sealed record DistrictUploadClassificationResult(
    DistrictUploadClassificationStatus Status,
    string? SourceUrl = null,
    string? ErrorMessage = null)
{
    public static DistrictUploadClassificationResult DirectData() =>
        new(DistrictUploadClassificationStatus.DirectData);

    public static DistrictUploadClassificationResult NetworkLink(string sourceUrl) =>
        new(DistrictUploadClassificationStatus.NetworkLink, sourceUrl);

    public static DistrictUploadClassificationResult InvalidFileType() =>
        new(DistrictUploadClassificationStatus.InvalidFileType);

    public static DistrictUploadClassificationResult InvalidFileContent(string? errorMessage = null) =>
        new(DistrictUploadClassificationStatus.InvalidFileContent, null, errorMessage);
}
