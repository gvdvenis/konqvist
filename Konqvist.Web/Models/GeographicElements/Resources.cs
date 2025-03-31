namespace Konqvist.Web.Models.GeographicElements;

public record Resources
{
    private readonly DistrictResourcesData _resourcesData;
    public static Resources Empty { get; } = new(new DistrictResourcesData());
    
    public Resources(DistrictResourcesData resourcesData)
    {
        _resourcesData = resourcesData;
    }

    public int Gold => _resourcesData.R1;
    public int Votes => _resourcesData.R2;
    public int People => _resourcesData.R3;
    public int Oil => _resourcesData.R4;

    public void Deconstruct(out DistrictResourcesData resourcesData)
    {
        resourcesData = _resourcesData;
    }

    public override string ToString()
    {
        return $"{nameof(Gold)}: {Gold}, {nameof(Votes)}: {Votes}, {nameof(People)}: {People}, {nameof(Oil)}: {Oil}";
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
