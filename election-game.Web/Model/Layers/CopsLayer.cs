using OpenLayers.Blazor;

namespace ElectionGame.Web.Model;

public class CopsLayer : ReactiveLayer
{
    #region Overrides of ReactiveLayer

    /// <inheritdoc />
    protected override bool IncludeInLayer(Shape shape)
    {
        return shape is Cop;
    }

    #endregion
}