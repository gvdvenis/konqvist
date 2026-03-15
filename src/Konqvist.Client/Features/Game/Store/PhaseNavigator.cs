using Fluxor;
using Konqvist.Client.Core.Models;
using Konqvist.Client.Core.State;
using Konqvist.Client.Features.Player.Store;
using Microsoft.AspNetCore.Components;

namespace Konqvist.Client.Features.Game.Store;

public sealed class PhaseNavigator(
    NavigationManager navigationManager,
    IState<PlayerState> playerState)
{
    [EffectMethod]
    public Task HandleNavigateToPhaseAction(NavigateToPhaseAction action, IDispatcher dispatcher)
    {
        var role = action.Role ?? playerState.Value.Role;
        var targetRoute = ResolveTargetRoute(action.CurrentPhase, role);
        if (targetRoute is null)
        {
            return Task.CompletedTask;
        }

        var currentRoute = GetCurrentRoute();
        if (!string.Equals(currentRoute, targetRoute, StringComparison.OrdinalIgnoreCase))
        {
            navigationManager.NavigateTo(targetRoute);
        }

        return Task.CompletedTask;
    }

    [EffectMethod]
    public Task HandleFullStateSyncAction(FullStateSyncAction action, IDispatcher dispatcher)
    {
        dispatcher.Dispatch(new NavigateToPhaseAction(
            action.Snapshot.Game.CurrentPhase,
            action.Snapshot.Player.Role));

        return Task.CompletedTask;
    }

    private string? ResolveTargetRoute(GamePhase phase, PlayerRole? role)
        => PhaseRouting.ResolveTargetRoute(phase, role);

    private string GetCurrentRoute()
        => PhaseRouting.NormalizeRoute(navigationManager.ToBaseRelativePath(navigationManager.Uri));
}
