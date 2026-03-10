namespace Konqvist.Admin.Features.Teams;

public enum BulkReplaceTeamTemplateResult
{
    Replaced = 0,
    TemplateNotFound = 1,
    InvalidTeamCount = 2,
    DuplicateName = 3,
    TokenGenerationFailed = 4
}
