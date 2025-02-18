using OpenLayers.Blazor;

namespace ElectionGame.Web.Model;

public abstract class ReactiveLayer : Layer
{
    protected ReactiveLayer()
    {
        Id = Guid.NewGuid().ToString();
        LayerType = LayerType.Vector;
        SourceType = SourceType.Vector;
        RaiseShapeEvents = true;
        SelectionEnabled = false;
    }
}