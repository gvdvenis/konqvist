namespace Konqvist.Admin.Features.Rounds;

public sealed record RoundTemplateManagementSnapshot(
    int TemplateId,
    string TemplateName,
    IReadOnlyList<RoundTemplateListItem> Rounds);
