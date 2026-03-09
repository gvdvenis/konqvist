using Konqvist.Infrastructure.Entities.Enums;
using Konqvist.Infrastructure.Entities.Template;

namespace Konqvist.Infrastructure.Entities.Session;

public class GameSession
{
    public int Id { get; set; }
    public int GameTemplateId { get; set; }
    public GameStatus Status { get; set; }
    public GamePhase CurrentPhase { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int? CurrentRoundSessionId { get; set; }

    public GameTemplate GameTemplate { get; set; } = null!;
    public RoundSession? CurrentRoundSession { get; set; }
    public ICollection<TeamSession> Teams { get; set; } = [];
    public ICollection<PlayerSession> Players { get; set; } = [];
    public ICollection<DistrictSession> Districts { get; set; } = [];
    public ICollection<RoundSession> Rounds { get; set; } = [];
    public ICollection<GameEvent> Events { get; set; } = [];
}
