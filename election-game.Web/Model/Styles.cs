using OpenLayers.Blazor;

namespace ElectionGame.Web.Model;

public static class MapStyles
{
    public static StyleOptions SelectedDistrictStyle { get; } = new SelectedDistrictStyle();
    public static StyleOptions DistrictTriggerStyle { get; } = new DistritcTriggerStyle();
    public static StyleOptions DistrictOwnerStyle(string owner)
    {
        return new DistrictOwnerStyle(owner);
    }
}

file class DistritcTriggerStyle : StyleOptions
{
    public DistritcTriggerStyle()
    {
        ZIndex = 2000;
        Stroke = new StrokeOptions
        {
            Color = "red",
            Width = 5
        };
        Fill = null;
    }
}

file class DistrictOwnerStyle : StyleOptions
{
    public DistrictOwnerStyle(string owner)
    {
        ZIndex = 1000;
        Stroke = new StrokeOptions
        {
            Color = "black",
            Width = 2,
            LineDashOffset = 2,
            LineDash = [4]
        };
        Fill = new FillOptions
        {
            Color = owner switch
            {
                "red" => "rgba(255, 50, 50, 0.5)",
                "blue" => "rgba(50, 50, 255, 0.5)",
                "green" => "rgba(50, 255, 50, 0.5)",
                _ => "rgba(50, 50, 50, 0.5)"
            }
        };
    }
}

file class SelectedDistrictStyle : StyleOptions
{
    public SelectedDistrictStyle()
    {
        ZIndex = 2000;
        Stroke = new StrokeOptions
        {
            Color = "gold",
            Width = 5
        };
        Fill = null;
    }
}