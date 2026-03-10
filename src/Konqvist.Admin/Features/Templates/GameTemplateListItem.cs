namespace Konqvist.Admin.Features.Templates;

public sealed record GameTemplateListItem(
    int Id,
    string Name,
    int TotalRounds,
    int LocationUpdateIntervalSeconds,
    int MinLocationUpdateIntervalSeconds,
    int VotingDurationSeconds,
    int PredictionBonusPoints,
    int VoteTimeoutPenalty,
    double DistrictCaptureRadiusMeters,
    int LinkedSessionCount)
{
    public bool HasLinkedSessions => LinkedSessionCount > 0;
}
