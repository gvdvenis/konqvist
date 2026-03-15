using Konqvist.Client.Core.State;

namespace Konqvist.Client.Features.Scores.Store;

public sealed record ScoreUpdatedAction(
    int TeamSessionId,
    int TotalScore,
    TeamResourceTotals Resources);
