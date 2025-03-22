using OpenLayers.Blazor;
using System.ComponentModel.Design;
using election_game.Data.Models;

namespace ElectionGame.Web.Model;

public abstract class ReactiveLayer : Layer
{
    protected ReactiveLayer()
    {
        Id = Guid.NewGuid().ToString();
        LayerType = LayerType.Vector;
        SourceType = SourceType.Vector;
        RaiseShapeEvents = true;
    }
}

public abstract class ReactiveLayer<TShapeData, TShape> : Layer where TShape : Shape where TShapeData : IShapeData
{

    protected ReactiveLayer()
    {
        Id = Guid.NewGuid().ToString();
        LayerType = LayerType.Vector;
        SourceType = SourceType.Vector;
        Projection = "EPSG:4326";
        RaiseShapeEvents = true;
    }

    public IEnumerable<TShape> Items => ShapesList.OfType<TShape>();
    
    /// <summary>
    ///     Call this method to initialize the layer with a list of shapeData.
    ///     It (re)creates all shapeData in <paramref name="shapeDataList"/> on the layer and takes
    ///     care op updating the <paramref name="gameMap"/> for you. It also enables Selection on the added
    ///     shapes if your provide an instance of <see cref="StyleOptions"/> to <paramref name="selectionStyles"/>.
    ///     If the layer is not yet added to the gameMap, it will be added first.
    /// </summary>
    /// <param name="shapeDataList"></param>
    /// <param name="gameMap"></param>
    /// <param name="selectionStyles"></param>
    /// <returns></returns>
    public async Task InitializeWithData(IEnumerable<TShapeData> shapeDataList, GameMap gameMap, StyleOptions? selectionStyles = null)
    {
        if (!gameMap.LayersList.Contains(this))
            gameMap.LayersList.Add(this);

        ShapesList.RemoveRange(ShapesList);

        var shapesToAdd = shapeDataList.SelectMany(ShapeInitializer);
        
        ShapesList.AddRange(shapesToAdd);

        await gameMap.SetSelectionSettings(this, SelectionEnabled, selectionStyles, false);
    }

    protected async Task AddOrReplaceShape(Shape? shape)
    {
        if (Map is null) return;
        if (shape is null) return;

        var existingShape = Items.FirstOrDefault(s => s == shape);

        if (existingShape is not null)
        {
            ShapesList.Remove(existingShape);
        }

        ShapesList.Add(shape);
        await Map.SetSelectionSettings(this, SelectionEnabled, MapStyles.SelectedDistrictStyle, false);
    }

    /// <summary>
    ///     This method is called by the <see cref="InitializeWithData"/> method for each item in the shapeData list.
    ///     It is responsible for creating the actual shape objects from the shapeData.
    /// </summary>
    /// <param name="shapeData"></param>
    /// <returns>A new array of shapes that will be added to the map.</returns>
    protected abstract Shape[] ShapeInitializer(TShapeData shapeData);
}
