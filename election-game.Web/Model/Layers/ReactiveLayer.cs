using OpenLayers.Blazor;
using System.Collections.Specialized;
using election_game.Data.Model.MapElements;

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
    private GameMap? _gameMap;

    protected ReactiveLayer()
    {
        Id = Guid.NewGuid().ToString();
        LayerType = LayerType.Vector;
        SourceType = SourceType.Vector;
        Projection = "EPSG:4326";
        RaiseShapeEvents = true;
        ShapesList.CollectionChanged += ShapesList_CollectionChanged;
    }

    public List<TShape> Items => ShapesList.OfType<TShape>().ToList();

    protected void SetGameMap(GameMap gameMap)
    {
        _gameMap = gameMap;
    }

    private void ShapesList_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_gameMap is null) throw new MissingMemberException("GameMap has to be set before you can add shapes. Call the SetGameMap(GameMap) protected method first");

        // first remove existing Districts from the maps list
        if (e.OldItems != null)
            _gameMap.ShapesList.RemoveRange(e.OldItems.Cast<Shape>());

        // now add the e.NewItems to the _maps shapesList
        if (e.NewItems != null)
            _gameMap.ShapesList.AddRange(e.NewItems.Cast<Shape>());
    }

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
        SetGameMap(gameMap);

        if (!gameMap.LayersList.Contains(this))
            gameMap.LayersList.Add(this);

        ShapesList.RemoveRange(ShapesList);

        var shapesToAdd = shapeDataList.SelectMany(ShapeInitializer);
        
        ShapesList.AddRange(shapesToAdd);

        await gameMap.UpdateLayer(this);
        await gameMap.SetSelectionSettings(this, SelectionEnabled, selectionStyles, false);
    }

    /// <summary>
    ///     This method is called by the <see cref="InitializeWithData"/> method for each item in the shapeData list.
    ///     It is responsible for creating the actual shape objects from the shapeData.
    /// </summary>
    /// <param name="shapeData"></param>
    /// <returns>A new array of shapes that will be added to the map.</returns>
    protected abstract Shape[] ShapeInitializer(TShapeData shapeData);
}
