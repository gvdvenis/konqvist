using System.Collections.Specialized;
using Microsoft.AspNetCore.Components;
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
        OnShapeAdded =  EventCallback.Factory.Create<Shape>(this, SetShapeProperties);
        ShapesList.CollectionChanged += ShapesListChanged;
    }

    private void ShapesListChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        e.NewItems?.Cast<Shape>().ToList().ForEach(SetShapeProperties);
    }
    
    /// <summary>
    ///     This method is called when a shape is added to the layer.
    ///     Return true if the shape should be included in the layer, false otherwise. 
    /// </summary>
    /// <param name="shape"></param>
    /// <returns></returns>
    protected abstract bool IncludeInLayer(Shape shape);
    
    private void SetShapeProperties(Shape obj)
    {
        try
        {
            obj.Layer = IncludeInLayer(obj) ? this : Map?.ShapesLayer;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public Task Hide()
    {
        Map = null;
        Visibility = false;
        return UpdateLayer();
    }

    public Task Show()
    {
        Visibility = true;
        return UpdateLayer();
    }
}