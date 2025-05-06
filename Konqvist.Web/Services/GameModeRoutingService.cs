using Microsoft.AspNetCore.Components.Routing;

namespace Konqvist.Web.Services;

public class GameModeRoutingService(
    NavigationManager navigationManager,
    SessionProvider sessionProvider,
    MapDataStore roundDataStore)
{
    public async Task TryNavigateToGameMode()
    {

        //if (sessionProvider.Session.IsAuthenticated == false)
        //{
        //    TryNavigate("login");
        //    return;
        //}
        
        if (sessionProvider.Session.IsAdmin) return;
        var appState = await roundDataStore.GetCurrentAppState();

        switch (appState)
        {
            case RoundKind.Voting:
                TryNavigate("voting");
                break;
            case RoundKind.GameOver:
                TryNavigate("gameover");
                break;
            case RoundKind.NotStarted:
                TryNavigate("waitforstart");
                break;
            case RoundKind.GatherResources:
                TryNavigate("map");
                break;
            default:
                if (sessionProvider.Session.IsAdmin) TryNavigate("management");
                break;
        }
    }

    private void TryNavigate(string page)
    {
        if (navigationManager.Uri.Contains(page))
            return;

        navigationManager.NavigateTo($"/{page}");
    }
}
