namespace Konqvist.Web.Models.GeographicElements;

public record Resources
{
    private readonly ResourcesData _resourcesData;
    public static Resources Empty { get; } = new(new ResourcesData());
    public string? ResourceNameOfInterest { get; }

    public Resources(ResourcesData resourcesData)
    {
        _resourcesData = resourcesData;
    }

    public int Gold => _resourcesData.R1;
    public int Votes => _resourcesData.R2;
    public int People => _resourcesData.R3;
    public int Oil => _resourcesData.R4;

    public void Deconstruct(out ResourcesData resourcesData)
    {
        resourcesData = _resourcesData;
    }

    public override string ToString()
    {
        return $"{nameof(Gold)}: {Gold}, {nameof(Votes)}: {Votes}, {nameof(People)}: {People}, {nameof(Oil)}: {Oil}";
    }

    public static string? ToResourceName(string resourceDataName)
    {
        return resourceDataName switch
        {
            nameof(ResourcesData.R1) => nameof(Gold),
            nameof(ResourcesData.R2) => nameof(Votes),
            nameof(ResourcesData.R3) => nameof(People),
            nameof(ResourcesData.R4) => nameof(Oil),
            _ => null
        };
    }

    public Dictionary<string, int> ToDictionary()
    {
        return new Dictionary<string, int>
        {
            {nameof(Gold), Gold},
            {nameof(Votes), Votes},
            {nameof(People), People},
            {nameof(Oil), Oil}
        };
    }
}
