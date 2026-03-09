using Konqvist.Infrastructure.Entities.Session;

namespace Konqvist.Infrastructure.Entities.Template;

public class GameTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TotalRounds { get; set; }
    public int LocationUpdateIntervalSeconds { get; set; }
    public int MinLocationUpdateIntervalSeconds { get; set; }
    public int VotingDurationSeconds { get; set; }
    public int PredictionBonusPoints { get; set; }
    public int VoteTimeoutPenalty { get; set; }
    public double DistrictCaptureRadiusMeters { get; set; }

    public ICollection<TeamTemplate> Teams { get; set; } = [];
    public ICollection<DistrictTemplate> Districts { get; set; } = [];
    public ICollection<RoundTemplate> Rounds { get; set; } = [];
    public ICollection<GameSession> GameSessions { get; set; } = [];
}
