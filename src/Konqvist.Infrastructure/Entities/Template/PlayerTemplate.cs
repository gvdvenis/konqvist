using Konqvist.Infrastructure.Entities.Enums;
using Konqvist.Infrastructure.Entities.Session;

namespace Konqvist.Infrastructure.Entities.Template;

public class PlayerTemplate
{
    public int Id { get; set; }
    public int TeamTemplateId { get; set; }
    public string LoginToken { get; set; } = string.Empty;
    public PlayerRole Role { get; set; }

    public TeamTemplate TeamTemplate { get; set; } = null!;
    public ICollection<PlayerSession> PlayerSessions { get; set; } = [];
}
