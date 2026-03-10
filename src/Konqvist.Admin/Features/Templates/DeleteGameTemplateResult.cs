namespace Konqvist.Admin.Features.Templates;

public enum DeleteGameTemplateResult
{
    Deleted = 0,
    NotFound = 1,
    HasLinkedSessions = 2
}
