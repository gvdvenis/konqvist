using Konqvist.Infrastructure.Entities.Enums;
using Konqvist.Infrastructure.Entities.Session;

namespace Konqvist.Infrastructure.Entities.Template;

public class RoundTemplate
{
    public int Id { get; set; }
    public int GameTemplateId { get; set; }
    public int RoundNumber { get; set; }
    public ResourceType RoiResource { get; set; }
    public string Stake { get; set; } = string.Empty;

    public GameTemplate GameTemplate { get; set; } = null!;
    public ICollection<RoundSession> RoundSessions { get; set; } = [];
}
