using Fluxor;
using Konqvist.Client.Core.State;

namespace Konqvist.Client.Features.Scores.Store;

public static class ScoresReducers
{
    [ReducerMethod]
    public static ScoresState ReduceScoreUpdatedAction(ScoresState state, ScoreUpdatedAction action)
    {
        var teamScores = new Dictionary<int, int>(state.TeamScores)
        {
            [action.TeamSessionId] = action.TotalScore
        };
        var teamResources = new Dictionary<int, TeamResourceTotals>(state.TeamResources)
        {
            [action.TeamSessionId] = action.Resources
        };

        return state with
        {
            TeamScores = teamScores,
            TeamResources = teamResources
        };
    }

    [ReducerMethod]
    public static ScoresState ReduceFullStateSyncAction(ScoresState state, FullStateSyncAction action) =>
        state with
        {
            TeamScores = new Dictionary<int, int>(action.Snapshot.Scores.TeamScores),
            TeamResources = new Dictionary<int, TeamResourceTotals>(action.Snapshot.Scores.TeamResources)
        };
}
