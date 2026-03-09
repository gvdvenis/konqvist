using Konqvist.Infrastructure.Entities.Session;

namespace Konqvist.Infrastructure.Entities.Template;

public class DistrictTemplate
{
    public int Id { get; set; }
    public int GameTemplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string GeoJson { get; set; } = string.Empty;
    public double TriggerLat { get; set; }
    public double TriggerLng { get; set; }
    public double? TriggerRadiusMeters { get; set; }
    public int Gold { get; set; }
    public int Voters { get; set; }
    public int Likes { get; set; }
    public int Oil { get; set; }

    public GameTemplate GameTemplate { get; set; } = null!;
    public ICollection<DistrictSession> DistrictSessions { get; set; } = [];
}
