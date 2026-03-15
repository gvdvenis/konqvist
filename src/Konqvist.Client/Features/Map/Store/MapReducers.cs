using Fluxor;
using Konqvist.Client.Core.State;

namespace Konqvist.Client.Features.Map.Store;

public static class MapReducers
{
    [ReducerMethod]
    public static MapState ReduceDistrictClaimedAction(MapState state, DistrictClaimedAction action)
    {
        var districtOwners = new Dictionary<int, int?>(state.DistrictOwners)
        {
            [action.DistrictSessionId] = action.TeamSessionId
        };

        return state with { DistrictOwners = districtOwners };
    }

    [ReducerMethod]
    public static MapState ReduceDistrictOwnershipChangedAction(MapState state, DistrictOwnershipChangedAction action)
    {
        var districtOwners = new Dictionary<int, int?>(state.DistrictOwners)
        {
            [action.DistrictSessionId] = action.TeamSessionId
        };

        return state with { DistrictOwners = districtOwners };
    }

    [ReducerMethod]
    public static MapState ReduceLocationUpdatedAction(MapState state, LocationUpdatedAction action)
    {
        var runnerPositions = new Dictionary<int, RunnerPosition>(state.RunnerPositions)
        {
            [action.PlayerSessionId] = new RunnerPosition(action.Latitude, action.Longitude)
        };

        return state with { RunnerPositions = runnerPositions };
    }

    [ReducerMethod]
    public static MapState ReduceFullStateSyncAction(MapState state, FullStateSyncAction action) =>
        state with
        {
            DistrictOwners = new Dictionary<int, int?>(action.Snapshot.Map.DistrictOwners),
            RunnerPositions = new Dictionary<int, RunnerPosition>(action.Snapshot.Map.RunnerPositions)
        };
}
