namespace Konqvist.Web.Services;

public class GameModeRoutingService(
    NavigationManager navigationManager,
    SessionProvider sessionProvider,
    MapDataStore roundDataStore)
{

    public async Task NavigateToGameMode()
    {

        if (sessionProvider.Session.IsAuthenticated == false)
        {
            TryNavigate("login");
            return;
        }

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
        }
    }

    private void TryNavigate(string page)
    {
        if (navigationManager.Uri.Contains(page))
            return;

        navigationManager.NavigateTo($"/{page}");
    }
}
