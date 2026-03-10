namespace Konqvist.Admin.Features.Teams;

public sealed record TeamTemplateManagementSnapshot(
    int TemplateId,
    string TemplateName,
    IReadOnlyList<TeamTemplateListItem> Teams);
