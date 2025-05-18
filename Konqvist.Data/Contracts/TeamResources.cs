using Konqvist.Data.Models;

namespace Konqvist.Data.Contracts;

public record TeamResources(
    TeamData Team, 
    ResourcesData AdditionalResources, 
    ResourcesData DistrictResources, 
    string? RelevantResourceName)
{
    public int GetScore()
    {
        return RelevantResourceName is null
            ? 0
            : AdditionalResources.GetScore(RelevantResourceName) + DistrictResources.GetScore(RelevantResourceName);
    }
}