using Konqvist.Infrastructure.Entities.Enums;
using Konqvist.Infrastructure.Entities.Template;

namespace Konqvist.Infrastructure.Entities.Session;

public class RoundSession
{
    public int Id { get; set; }
    public int GameSessionId { get; set; }
    public int RoundTemplateId { get; set; }
    public RoundStatus Status { get; set; }
    public bool VotingEnabled { get; set; }
    public DateTime? VotingStartedAt { get; set; }
    public int? WinnerTeamSessionId { get; set; }

    public GameSession GameSession { get; set; } = null!;
    public RoundTemplate RoundTemplate { get; set; } = null!;
    public TeamSession? WinnerTeamSession { get; set; }
    public ICollection<Vote> Votes { get; set; } = [];
    public ICollection<RoundSnapshot> Snapshots { get; set; } = [];
    public ICollection<DistrictOwnershipSnapshot> OwnershipSnapshots { get; set; } = [];
    public ICollection<GameEvent> Events { get; set; } = [];
}
