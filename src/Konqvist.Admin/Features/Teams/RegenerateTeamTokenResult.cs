namespace Konqvist.Admin.Features.Teams;

public enum RegenerateTeamTokenResult
{
    Regenerated = 0,
    TemplateNotFound = 1,
    TeamNotFound = 2,
    PlayerNotFound = 3,
    TokenGenerationFailed = 4
}
