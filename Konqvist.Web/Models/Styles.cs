using System.Drawing;
using System.Globalization;

namespace Konqvist.Web.Models;

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
        string semitransparentColor = ConvertToSemitransparent(ownerColor, 0.7);

        Stroke = new StrokeOptions
        {
            Color = "black",
            Width = 2,
            LineDashOffset = 2,
            LineDash = [4]
        };
        Fill = new FillOptions
        {
            Color = semitransparentColor
        };
    }

    private static string ConvertToSemitransparent(string color, double opacity)
    {
        // Ensure opacity is within the valid range (0.0 to 1.0)
        opacity = Math.Clamp(opacity, 0.0, 1.0);

        try
        {
            // Assuming the input is a valid HTML color (e.g., "#RRGGBB" or "rgb(r, g, b)")
            Color parsedColor = ColorTranslator.FromHtml(color);

            opacity = parsedColor.A == 0 ? 0 : opacity;

            // Use InvariantCulture to ensure the opacity is formatted with a decimal point
            return $"rgba({parsedColor.R}, {parsedColor.G}, {parsedColor.B}, {opacity.ToString(CultureInfo.InvariantCulture)})";
        }
        catch
        {
            // Fallback to a default semitransparent color if parsing fails
            return $"rgba(0, 0, 0, {opacity.ToString(CultureInfo.InvariantCulture)})";
        }
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