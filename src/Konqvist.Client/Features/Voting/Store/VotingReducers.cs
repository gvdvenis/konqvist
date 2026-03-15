using Fluxor;
using Konqvist.Client.Core.State;

namespace Konqvist.Client.Features.Voting.Store;

public static class VotingReducers
{
    [ReducerMethod]
    public static VotingState ReduceVotingOpenedAction(VotingState state, VotingOpenedAction action) =>
        state with
        {
            IsVotingOpen = true,
            TimeRemaining = action.TimeRemaining,
            VotesPerTeam = new Dictionary<int, int>()
        };

    [ReducerMethod]
    public static VotingState ReduceVoteCastAction(VotingState state, VoteCastAction action)
    {
        var votesPerTeam = new Dictionary<int, int>(state.VotesPerTeam);
        votesPerTeam[action.TargetTeamSessionId] =
            votesPerTeam.GetValueOrDefault(action.TargetTeamSessionId) + action.VoteValue;

        return state with { VotesPerTeam = votesPerTeam };
    }

    [ReducerMethod]
    public static VotingState ReduceVotingClosedAction(VotingState state, VotingClosedAction _) =>
        state with
        {
            IsVotingOpen = false,
            TimeRemaining = null
        };

    [ReducerMethod]
    public static VotingState ReduceVotingTimerUpdatedAction(VotingState state, VotingTimerUpdatedAction action) =>
        state with { TimeRemaining = action.TimeRemaining };

    [ReducerMethod]
    public static VotingState ReduceFullStateSyncAction(VotingState state, FullStateSyncAction action) =>
        state with
        {
            VotesPerTeam = new Dictionary<int, int>(action.Snapshot.Voting.VotesPerTeam),
            IsVotingOpen = action.Snapshot.Voting.IsVotingOpen,
            TimeRemaining = action.Snapshot.Voting.TimeRemaining
        };
}
