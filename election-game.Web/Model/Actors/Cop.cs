using OpenLayers.Blazor;

namespace ElectionGame.Web.Model;

public class Cop : Actor
{
    public Cop(Coordinate position) : base(position)
    {
        PinColor = PinColor.Blue;
    }
}