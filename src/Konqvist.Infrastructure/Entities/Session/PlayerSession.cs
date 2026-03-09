using Konqvist.Infrastructure.Entities.Template;

namespace Konqvist.Infrastructure.Entities.Session;

public class PlayerSession
{
    public int Id { get; set; }
    public int GameSessionId { get; set; }
    public int PlayerTemplateId { get; set; }
    public bool IsLoggedIn { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastSeen { get; set; }
    public double? LocationLat { get; set; }
    public double? LocationLng { get; set; }
    public DateTime? LocationUpdatedAt { get; set; }

    public GameSession GameSession { get; set; } = null!;
    public PlayerTemplate PlayerTemplate { get; set; } = null!;
    public ICollection<GameEvent> ActorEvents { get; set; } = [];
}
