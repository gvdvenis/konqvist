using Fluxor;
using Konqvist.Client.Core.State;

namespace Konqvist.Client.Features.Player.Store;

public static class PlayerReducers
{
    [ReducerMethod]
    public static PlayerState ReducePlayerIdentityLoadedAction(PlayerState state, PlayerIdentityLoadedAction action) =>
        state with
        {
            PlayerSessionId = action.PlayerSessionId,
            TeamSessionId = action.TeamSessionId,
            TeamName = action.TeamName,
            Role = action.Role,
            IsLoggedIn = action.IsLoggedIn,
            IsOnline = action.IsOnline
        };

    [ReducerMethod]
    public static PlayerState ReduceRunnerStateChangedAction(PlayerState state, RunnerStateChangedAction action) =>
        state.PlayerSessionId == action.PlayerSessionId
            ? state with
            {
                TeamSessionId = action.TeamSessionId,
                IsLoggedIn = action.IsLoggedIn,
                IsOnline = action.IsOnline
            }
            : state;

    [ReducerMethod]
    public static PlayerState ReduceRunnerLoggedOutAction(PlayerState state, RunnerLoggedOutAction action) =>
        state.PlayerSessionId == action.PlayerSessionId
            ? state with
            {
                IsLoggedIn = false,
                IsOnline = false
            }
            : state;

    [ReducerMethod]
    public static PlayerState ReduceFullStateSyncAction(PlayerState state, FullStateSyncAction action) =>
        state with
        {
            PlayerSessionId = action.Snapshot.Player.PlayerSessionId,
            TeamSessionId = action.Snapshot.Player.TeamSessionId,
            TeamName = action.Snapshot.Player.TeamName,
            Role = action.Snapshot.Player.Role,
            IsLoggedIn = action.Snapshot.Player.IsLoggedIn,
            IsOnline = action.Snapshot.Player.IsOnline
        };
}
