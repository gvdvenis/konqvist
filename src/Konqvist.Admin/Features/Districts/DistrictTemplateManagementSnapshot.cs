namespace Konqvist.Admin.Features.Districts;

public sealed record DistrictTemplateManagementSnapshot(
    int TemplateId,
    string TemplateName,
    int DistrictCount,
    string? DistrictImportSourceUrl);
