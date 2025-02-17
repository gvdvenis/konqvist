using OpenLayers.Blazor;

namespace ElectionGame.Web.Model;

public static class Helpers
{
    public static List<Team> AsTeamList(this ObservableRangeCollection<Shape>? markers)
    {
        return markers?.OfType<Team>().ToList() ?? [];
    }
}