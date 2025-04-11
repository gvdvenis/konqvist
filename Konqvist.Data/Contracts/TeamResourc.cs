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
        if (RelevantResourceName is null)
        {
            return 0;
        }

        return AdditionalResources.GetScore(RelevantResourceName) + DistrictResources.GetScore(RelevantResourceName);
    }
}