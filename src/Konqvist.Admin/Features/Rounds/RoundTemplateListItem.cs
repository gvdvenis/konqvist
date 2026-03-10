using Konqvist.Infrastructure.Entities.Enums;

namespace Konqvist.Admin.Features.Rounds;

public sealed record RoundTemplateListItem(
    int Id,
    int RoundNumber,
    ResourceType RoiResource,
    string Stake);
