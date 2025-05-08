namespace Konqvist.Web;

public class SessionKeyProvider
{
    public string GameInstanceKey { get; private set; } = Guid.NewGuid().ToString();

    public void InvalidateGameInstanceKey()
    {
        GameInstanceKey = Guid.NewGuid().ToString();
    }
}