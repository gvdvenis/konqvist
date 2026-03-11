using Konqvist.Infrastructure.Entities.Enums;
using Konqvist.Infrastructure.Entities.Template;

namespace Konqvist.Infrastructure.Persistence.SeedData;

internal static class TemplateSeedData
{
    public const int DemoGameTemplateId = 1;
    public const int AlphaTeamTemplateId = 1;
    public const int BravoTeamTemplateId = 2;

    public static readonly GameTemplate DemoGameTemplate = new()
    {
        Id = DemoGameTemplateId,
        Name = "Demo Game",
        GmLoginToken = "GM57t7",
        TotalRounds = 2,
        LocationUpdateIntervalSeconds = 30,
        MinLocationUpdateIntervalSeconds = 5,
        VotingDurationSeconds = 30,
        PredictionBonusPoints = 150,
        VoteTimeoutPenalty = 50,
        DistrictCaptureRadiusMeters = 15d
    };

    public static readonly TeamTemplate[] TeamTemplates =
    [
        new TeamTemplate
        {
            Id = AlphaTeamTemplateId,
            GameTemplateId = DemoGameTemplateId,
            Name = "Alpha",
            Color = "#1E88E5"
        },
        new TeamTemplate
        {
            Id = BravoTeamTemplateId,
            GameTemplateId = DemoGameTemplateId,
            Name = "Bravo",
            Color = "#E53935"
        }
    ];

    public static readonly PlayerTemplate[] PlayerTemplates =
    [
        new PlayerTemplate
        {
            Id = 1,
            TeamTemplateId = AlphaTeamTemplateId,
            LoginToken = "AR15ee",
            Role = PlayerRole.Runner
        },
        new PlayerTemplate
        {
            Id = 2,
            TeamTemplateId = AlphaTeamTemplateId,
            LoginToken = "ATC5y85",
            Role = PlayerRole.TeamLeader
        },
        new PlayerTemplate
        {
            Id = 3,
            TeamTemplateId = BravoTeamTemplateId,
            LoginToken = "BR2q9L",
            Role = PlayerRole.Runner
        },
        new PlayerTemplate
        {
            Id = 4,
            TeamTemplateId = BravoTeamTemplateId,
            LoginToken = "BTC8k1M",
            Role = PlayerRole.TeamLeader
        }
    ];

    public static readonly RoundTemplate[] RoundTemplates =
    [
        new RoundTemplate
        {
            Id = 1,
            GameTemplateId = DemoGameTemplateId,
            RoundNumber = 1,
            RoiResource = ResourceType.Gold,
            Stake = "Gold grant doubles for the winning team this round."
        },
        new RoundTemplate
        {
            Id = 2,
            GameTemplateId = DemoGameTemplateId,
            RoundNumber = 2,
            RoiResource = ResourceType.Voters,
            Stake = "Winning team gains a voter momentum bonus."
        }
    ];
}
