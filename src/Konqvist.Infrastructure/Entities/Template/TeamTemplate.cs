using Konqvist.Infrastructure.Entities.Session;

namespace Konqvist.Infrastructure.Entities.Template;

public class TeamTemplate
{
    public int Id { get; set; }
    public int GameTemplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;

    public GameTemplate GameTemplate { get; set; } = null!;
    public ICollection<PlayerTemplate> Players { get; set; } = [];
    public ICollection<TeamSession> TeamSessions { get; set; } = [];
}
