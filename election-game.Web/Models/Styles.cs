namespace ElectionGame.Web.Model;

public static class MapStyles
{
    public static StyleOptions SelectedDistrictStyle { get; } = new SelectedDistrictStyle();
    public static StyleOptions DistrictTriggerStyle { get; } = new DistritcTriggerStyle();
    public static StyleOptions DistrictOwnerStyle(string ownerColor)
    {
        return new DistrictOwnerStyle(ownerColor);
    }
}

file class DistritcTriggerStyle : StyleOptions
{
    public DistritcTriggerStyle()
    {
        Stroke = new StrokeOptions
        {
            Color = "red",
            Width = 3
        };
        Fill = null;
    }
}

file class DistrictOwnerStyle : StyleOptions
{
    public DistrictOwnerStyle(string ownerColor)
    {
        Stroke = new StrokeOptions
        {
            Color = "black",
            Width = 2,
            LineDashOffset = 2,
            LineDash = [4]
        };
        Fill = new FillOptions
        {
            Color = ownerColor
            //    Color = ownerColor switch
            //{
            //    "red" => "rgba(255, 50, 50, 0.5)",
            //    "blue" => "rgba(50, 50, 255, 0.5)",
            //    "green" => "rgba(50, 255, 50, 0.5)",
            //    "magenta" => "rgba(255, 50, 255, 0.5)",
            //    "yellow" => "rgba(255, 255, 50, 0.5)",
            //    "cyan" => "rgba(50, 255, 255, 0.5)",
            //    _ => "rgba(50, 50, 50, 0.5)"
            //}
        };
    }
}

file class SelectedDistrictStyle : StyleOptions
{
    public SelectedDistrictStyle()
    {
        Stroke = new StrokeOptions
        {
            Color = "gold",
            Width = 2
        };
        Fill = new FillOptions()
        {
            Color = "rgba(0, 0, 0, 0.3)"
        };
    }
}