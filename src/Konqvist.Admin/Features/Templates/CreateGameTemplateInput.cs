namespace Konqvist.Admin.Features.Templates;

public sealed class CreateGameTemplateInput
{
    public string Name { get; set; } = string.Empty;
    public int LocationUpdateIntervalSeconds { get; set; } = 30;
    public int MinLocationUpdateIntervalSeconds { get; set; } = 5;
    public int VotingDurationSeconds { get; set; } = 30;
    public int PredictionBonusPoints { get; set; } = 150;
    public int VoteTimeoutPenalty { get; set; } = 50;
    public double DistrictCaptureRadiusMeters { get; set; } = 15d;
}
