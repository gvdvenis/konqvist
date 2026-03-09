using Konqvist.Infrastructure.Entities.Template;

namespace Konqvist.Infrastructure.Entities.Session;

public class TeamSession
{
    public int Id { get; set; }
    public int GameSessionId { get; set; }
    public int TeamTemplateId { get; set; }
    public int TotalScore { get; set; }
    public int TotalGold { get; set; }
    public int TotalVoters { get; set; }
    public int TotalLikes { get; set; }
    public int TotalOil { get; set; }

    public GameSession GameSession { get; set; } = null!;
    public TeamTemplate TeamTemplate { get; set; } = null!;
    public ICollection<DistrictSession> OwnedDistricts { get; set; } = [];
    public ICollection<RoundSession> WonRounds { get; set; } = [];
    public ICollection<Vote> VotesCast { get; set; } = [];
    public ICollection<Vote> VotesTargeted { get; set; } = [];
    public ICollection<RoundSnapshot> Snapshots { get; set; } = [];
    public ICollection<DistrictOwnershipSnapshot> OwnershipSnapshotsAsOwner { get; set; } = [];
}
