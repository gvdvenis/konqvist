using OpenLayers.Blazor;

namespace ElectionGame.Web.Model;

public class ReactiveLayer : Layer
{
    public ReactiveLayer()
    {
        Id = Guid.NewGuid().ToString();
        LayerType = LayerType.Vector;
        SourceType = SourceType.Vector;
        RaiseShapeEvents = true;
        SelectionEnabled = false;
    }

    public void Hide()
    {
        Visibility = false;
    }

    public void Show()
    {
        Visibility = true;
    }
}

public class DistrictsLayer: Layer
{
    public DistrictsLayer(Func<Shape, StyleOptions?> getShapeStyle, string shapeData)
    {
        Id = "districts";
        LayerType = LayerType.Vector;
        SourceType = SourceType.VectorGeoJson;
        RaiseShapeEvents = true;
        Projection = "EPSG:4326";
        StyleCallback = getShapeStyle;
        SelectionEnabled = true;
        Declutter = true;
        Data = shapeData;
    }

    public List<District> Districts => ShapesList.Select(s=>new District(s.Coordinates, s.Properties)).ToList();
}