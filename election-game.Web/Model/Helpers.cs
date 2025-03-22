using OpenLayers.Blazor;

namespace ElectionGame.Web.Model;

public static class Helpers
{
    public static List<Team> AsTeamList(this ObservableRangeCollection<Shape>? markers)
    {
        return markers?.OfType<Team>().ToList() ?? [];
    }

    public static IEnumerable<District> AsDistrictsList(this ObservableRangeCollection<Shape>? shapes)
    {
        return shapes?.OfType<District>() ?? [];
    }

    public static void RemoveActorsOfType<TActor>(this GameMap map) where TActor: Actor
    {
        var cops = map.FeaturesList.OfType<TActor>().ToList();

        if (cops.Count <= 0) return;

        map.ShapesList.RemoveRange(cops);
        map.MarkersList.RemoveRange(cops);
    }
}