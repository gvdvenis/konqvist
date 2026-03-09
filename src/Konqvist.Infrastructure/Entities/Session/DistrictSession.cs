using Konqvist.Infrastructure.Entities.Template;

namespace Konqvist.Infrastructure.Entities.Session;

public class DistrictSession
{
    public int Id { get; set; }
    public int GameSessionId { get; set; }
    public int DistrictTemplateId { get; set; }
    public int? CurrentOwnerTeamSessionId { get; set; }
    public bool IsClaimedThisRound { get; set; }
    public DateTime? LastClaimedAt { get; set; }

    public GameSession GameSession { get; set; } = null!;
    public DistrictTemplate DistrictTemplate { get; set; } = null!;
    public TeamSession? CurrentOwnerTeamSession { get; set; }
    public ICollection<DistrictOwnershipSnapshot> OwnershipSnapshots { get; set; } = [];
}
