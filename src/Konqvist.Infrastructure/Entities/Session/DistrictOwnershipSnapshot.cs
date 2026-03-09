using Konqvist.Infrastructure.Entities.Enums;

namespace Konqvist.Infrastructure.Entities.Session;

public class DistrictOwnershipSnapshot
{
    public int Id { get; set; }
    public int RoundSessionId { get; set; }
    public int DistrictSessionId { get; set; }
    public int? OwnerTeamSessionId { get; set; }
    public SnapshotPhase Phase { get; set; }
    public DateTime SnapshotTaken { get; set; }

    public RoundSession RoundSession { get; set; } = null!;
    public DistrictSession DistrictSession { get; set; } = null!;
    public TeamSession? OwnerTeamSession { get; set; }
}
