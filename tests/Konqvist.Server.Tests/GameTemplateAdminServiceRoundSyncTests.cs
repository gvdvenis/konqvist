using Konqvist.Admin.Features.Rounds;
using Konqvist.Admin.Features.Templates;
using Microsoft.EntityFrameworkCore;

namespace Konqvist.Server.Tests;

public sealed class GameTemplateAdminServiceRoundSyncTests
{
    [Fact]
    public async Task CreateAsync_InitializesDefaultRoundsAndDerivedTotalRounds()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.TemplateService.CreateAsync(new CreateGameTemplateInput
        {
            Name = "Create Service Test",
            LocationUpdateIntervalSeconds = 30,
            MinLocationUpdateIntervalSeconds = 5,
            VotingDurationSeconds = 30,
            PredictionBonusPoints = 150,
            VoteTimeoutPenalty = 50,
            DistrictCaptureRadiusMeters = 50d
        });

        await using var dbContext = await harness.DbFactory.CreateDbContextAsync();
        var template = await dbContext.GameTemplates
            .Include(entity => entity.Rounds)
            .SingleAsync(entity => entity.Id == templateId);

        Assert.Equal(RoundTemplateDefaults.DefaultRoundCount, template.Rounds.Count);
        Assert.Equal(template.Rounds.Count, template.TotalRounds);
        Assert.Equal([1, 2, 3, 4], template.Rounds.OrderBy(round => round.RoundNumber).Select(round => round.RoundNumber).ToArray());
    }

    [Fact]
    public async Task UpdateAsync_KeepsTotalRoundsDerivedFromExistingRounds()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.TemplateService.CreateAsync(new CreateGameTemplateInput
        {
            Name = "Update Service Test",
            LocationUpdateIntervalSeconds = 30,
            MinLocationUpdateIntervalSeconds = 5,
            VotingDurationSeconds = 30,
            PredictionBonusPoints = 150,
            VoteTimeoutPenalty = 50,
            DistrictCaptureRadiusMeters = 50d
        });
        await harness.RoundService.AddRoundAsync(templateId);

        var updateResult = await harness.TemplateService.UpdateAsync(templateId, new CreateGameTemplateInput
        {
            Name = "Update Service Test v2",
            LocationUpdateIntervalSeconds = 40,
            MinLocationUpdateIntervalSeconds = 10,
            VotingDurationSeconds = 35,
            PredictionBonusPoints = 175,
            VoteTimeoutPenalty = 70,
            DistrictCaptureRadiusMeters = 55d
        });
        Assert.Equal(SaveGameTemplateResult.Saved, updateResult);

        await using var dbContext = await harness.DbFactory.CreateDbContextAsync();
        var template = await dbContext.GameTemplates
            .Include(entity => entity.Rounds)
            .SingleAsync(entity => entity.Id == templateId);

        Assert.Equal(5, template.Rounds.Count);
        Assert.Equal(5, template.TotalRounds);
        Assert.Equal("Update Service Test v2", template.Name);
        Assert.Equal(40, template.LocationUpdateIntervalSeconds);
    }
}
