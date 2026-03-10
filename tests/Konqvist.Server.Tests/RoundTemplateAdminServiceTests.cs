using Konqvist.Admin.Features.Rounds;
using Konqvist.Infrastructure.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace Konqvist.Server.Tests;

public sealed class RoundTemplateAdminServiceTests
{
    [Fact]
    public async Task GetAsync_WhenTemplateHasNoRounds_InitializesDefaultsAndSyncsTotalRounds()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.CreateTemplateAsync(totalRounds: 0);

        var snapshot = await harness.RoundService.GetAsync(templateId);

        Assert.NotNull(snapshot);
        Assert.Equal(RoundTemplateDefaults.DefaultRoundCount, snapshot.Rounds.Count);
        Assert.Equal([1, 2, 3, 4], snapshot.Rounds.Select(round => round.RoundNumber).ToArray());
        Assert.All(snapshot.Rounds, round =>
        {
            Assert.Contains(round.RoiResource, Enum.GetValues<ResourceType>());
            Assert.Equal(
                RoundTemplateDefaults.BuildDefaultStake(round.RoundNumber),
                round.Stake);
        });

        await using var dbContext = await harness.DbFactory.CreateDbContextAsync();
        var totalRounds = await dbContext.GameTemplates
            .Where(entity => entity.Id == templateId)
            .Select(entity => entity.TotalRounds)
            .SingleAsync();
        Assert.Equal(RoundTemplateDefaults.DefaultRoundCount, totalRounds);
    }

    [Fact]
    public async Task AddAndDeleteRound_ReindexesRoundsAndKeepsDerivedTotalRounds()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.CreateTemplateWithRoundsAsync(1, 2);

        var addResult = await harness.RoundService.AddRoundAsync(templateId);
        Assert.Equal(AddRoundTemplateResult.Added, addResult);

        var afterAddSnapshot = await harness.RoundService.GetAsync(templateId);
        Assert.NotNull(afterAddSnapshot);
        Assert.Equal([1, 2, 3], afterAddSnapshot.Rounds.Select(round => round.RoundNumber).ToArray());

        var roundToDeleteId = afterAddSnapshot.Rounds.Single(round => round.RoundNumber == 2).Id;
        var deleteResult = await harness.RoundService.DeleteAsync(templateId, roundToDeleteId);
        Assert.Equal(DeleteRoundTemplateResult.Deleted, deleteResult);

        var afterDeleteSnapshot = await harness.RoundService.GetAsync(templateId);
        Assert.NotNull(afterDeleteSnapshot);
        Assert.Equal([1, 2], afterDeleteSnapshot.Rounds.Select(round => round.RoundNumber).ToArray());

        await using var dbContext = await harness.DbFactory.CreateDbContextAsync();
        var totalRounds = await dbContext.GameTemplates
            .Where(entity => entity.Id == templateId)
            .Select(entity => entity.TotalRounds)
            .SingleAsync();
        Assert.Equal(2, totalRounds);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsExpectedResultForMissingAndLastRound()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.CreateTemplateWithRoundsAsync(1);
        var snapshot = await harness.RoundService.GetAsync(templateId);
        Assert.NotNull(snapshot);
        var roundId = snapshot.Rounds.Single().Id;

        var missingResult = await harness.RoundService.DeleteAsync(templateId, int.MaxValue);
        Assert.Equal(DeleteRoundTemplateResult.RoundNotFound, missingResult);

        var lastRoundResult = await harness.RoundService.DeleteAsync(templateId, roundId);
        Assert.Equal(DeleteRoundTemplateResult.LastRoundRequired, lastRoundResult);
    }

    [Fact]
    public async Task UpdateAsync_ValidatesStakeLengthAndTrimsSavedStake()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await harness.CreateTemplateWithRoundsAsync(1);
        var snapshot = await harness.RoundService.GetAsync(templateId);
        Assert.NotNull(snapshot);
        var roundId = snapshot.Rounds.Single().Id;

        var validResult = await harness.RoundService.UpdateAsync(
            templateId,
            roundId,
            new RoundTemplateEditorInput
            {
                RoiResource = ResourceType.Oil,
                Stake = $"  {new string('x', 500)}  "
            });
        Assert.Equal(SaveRoundTemplateResult.Saved, validResult);

        await using (var dbContext = await harness.DbFactory.CreateDbContextAsync())
        {
            var round = await dbContext.RoundTemplates.SingleAsync(entity => entity.Id == roundId);
            Assert.Equal(ResourceType.Oil, round.RoiResource);
            Assert.Equal(500, round.Stake.Length);
        }

        var tooLongResult = await harness.RoundService.UpdateAsync(
            templateId,
            roundId,
            new RoundTemplateEditorInput
            {
                RoiResource = ResourceType.Gold,
                Stake = new string('x', 501)
            });
        Assert.Equal(SaveRoundTemplateResult.InvalidInput, tooLongResult);

        var emptyResult = await harness.RoundService.UpdateAsync(
            templateId,
            roundId,
            new RoundTemplateEditorInput
            {
                RoiResource = ResourceType.Gold,
                Stake = "   "
            });
        Assert.Equal(SaveRoundTemplateResult.InvalidInput, emptyResult);
    }
}
