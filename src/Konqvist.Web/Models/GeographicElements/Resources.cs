using Microsoft.FluentUI.AspNetCore.Components;

namespace Konqvist.Web.Models.GeographicElements;

public record Resources
{
    private readonly ResourcesData _resourcesData;
    public static Resources Empty { get; } = new(new ResourcesData());

    public Resources(ResourcesData resourcesData)
    {
        _resourcesData = resourcesData;
    }

    public int Gold => _resourcesData.R1;
    public int Voters => _resourcesData.R2;
    public int Likes => _resourcesData.R3;
    public int Oil => _resourcesData.R4;

    public void Deconstruct(out ResourcesData resourcesData)
    {
        resourcesData = _resourcesData;
    }

    public override string ToString()
    {
        return $"{nameof(Gold)}: {Gold}, {nameof(Voters)}: {Voters}, {nameof(Likes)}: {Likes}, {nameof(Oil)}: {Oil}";
    }

    public static string? ToResourceName(string? resourceDataName)
    {
        return resourceDataName switch
        {
            nameof(ResourcesData.R1) => nameof(Gold),
            nameof(ResourcesData.R2) => nameof(Voters),
            nameof(ResourcesData.R3) => nameof(Likes),
            nameof(ResourcesData.R4) => nameof(Oil),
            _ => null
        };
    }

    public Dictionary<string, (int Amount, Icon Icon)> ToDictionary()
    {
        return new Dictionary<string, (int, Icon)>
        {
            {nameof(Gold), (Gold, new NavIcons.CoinStack())},
            {nameof(Voters), (Voters, new NavIcons.Vote()) } ,
            {nameof(Likes), (Likes, new NavIcons.ThumbLike()) },
            {nameof(Oil), (Oil, new NavIcons.Drop())}
        };
    }
}
