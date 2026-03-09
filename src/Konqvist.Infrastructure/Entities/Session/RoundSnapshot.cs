using Konqvist.Infrastructure.Entities.Enums;

namespace Konqvist.Infrastructure.Entities.Session;

public class RoundSnapshot
{
    public int Id { get; set; }
    public int RoundSessionId { get; set; }
    public int TeamSessionId { get; set; }
    public SnapshotPhase Phase { get; set; }
    public int Score { get; set; }
    public int Gold { get; set; }
    public int Voters { get; set; }
    public int Likes { get; set; }
    public int Oil { get; set; }
    public DateTime SnapshotTaken { get; set; }

    public RoundSession RoundSession { get; set; } = null!;
    public TeamSession TeamSession { get; set; } = null!;
}
