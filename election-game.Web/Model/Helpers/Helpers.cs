using OpenLayers.Blazor;

namespace ElectionGame.Web.Model.Helpers;

public static class Helpers
{

    public static List<Team> AsTeamList(this ObservableRangeCollection<Shape>? markers)
    {
        return markers?.OfType<Team>().ToList() ?? [];
    }
}
