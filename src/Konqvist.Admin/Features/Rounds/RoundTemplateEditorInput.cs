using Konqvist.Infrastructure.Entities.Enums;

namespace Konqvist.Admin.Features.Rounds;

public sealed class RoundTemplateEditorInput
{
    public ResourceType RoiResource { get; set; } = ResourceType.Gold;
    public string Stake { get; set; } = string.Empty;
}
