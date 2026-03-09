namespace Konqvist.Infrastructure.Entities.Session;

public class Vote
{
    public int Id { get; set; }
    public int RoundSessionId { get; set; }
    public int VotingTeamSessionId { get; set; }
    public int TargetTeamSessionId { get; set; }
    public int VoteValue { get; set; }
    public bool IsAutocast { get; set; }
    public DateTime CastAt { get; set; }

    public RoundSession RoundSession { get; set; } = null!;
    public TeamSession VotingTeamSession { get; set; } = null!;
    public TeamSession TargetTeamSession { get; set; } = null!;
}
