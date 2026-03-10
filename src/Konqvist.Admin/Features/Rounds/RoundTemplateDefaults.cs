using Konqvist.Infrastructure.Entities.Enums;
using Konqvist.Infrastructure.Entities.Template;

namespace Konqvist.Admin.Features.Rounds;

public static class RoundTemplateDefaults
{
    private static readonly IReadOnlyList<ResourceType> ResourceTypes = Enum.GetValues<ResourceType>();
    public const int DefaultRoundCount = 4;

    public static RoundTemplate Create(int roundNumber)
    {
        return new RoundTemplate
        {
            RoundNumber = roundNumber,
            RoiResource = ResourceTypes[Random.Shared.Next(ResourceTypes.Count)],
            Stake = BuildDefaultStake(roundNumber)
        };
    }

    public static string BuildDefaultStake(int roundNumber) => $"Configure stake for round {roundNumber}";
}
