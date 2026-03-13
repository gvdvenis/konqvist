using Konqvist.Infrastructure.Entities.Enums;

namespace Konqvist.Admin.Features.Session;

public sealed record SessionManagementSnapshot(
    IReadOnlyList<SessionTemplateOption> Templates,
    int? CurrentSessionId,
    int? CurrentTemplateId,
    string? CurrentTemplateName,
    GameStatus? CurrentStatus,
    DateTime? StartedAt);
