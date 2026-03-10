namespace Konqvist.Admin.Features.Rounds;

public enum DeleteRoundTemplateResult
{
    Deleted = 0,
    TemplateNotFound = 1,
    RoundNotFound = 2,
    LastRoundRequired = 3
}
