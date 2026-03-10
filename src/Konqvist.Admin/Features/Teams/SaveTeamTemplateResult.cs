namespace Konqvist.Admin.Features.Teams;

public enum SaveTeamTemplateResult
{
    Saved = 0,
    TemplateNotFound = 1,
    TeamNotFound = 2,
    DuplicateName = 3,
    InvalidInput = 4,
    TokenGenerationFailed = 5
}
