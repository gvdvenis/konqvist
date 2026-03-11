using Konqvist.Admin.Features.Rounds;
using Konqvist.Admin.Features.Templates;
using Konqvist.Infrastructure.Entities.Enums;
using Konqvist.Infrastructure.Entities.Template;
using Konqvist.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Konqvist.Server.Tests;

internal sealed class RoundConfigurationTestHarness
{
    private RoundConfigurationTestHarness(TestDbContextFactory dbFactory)
    {
        DbFactory = dbFactory;
        RoundService = new RoundTemplateAdminService(DbFactory);
        TemplateService = new GameTemplateAdminService(DbFactory);
    }

    public TestDbContextFactory DbFactory { get; }

    public RoundTemplateAdminService RoundService { get; }

    public GameTemplateAdminService TemplateService { get; }

    public static async Task<RoundConfigurationTestHarness> CreateAsync()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"konqvist-tests-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<KonqvistDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        var dbFactory = new TestDbContextFactory(options);

        await using (var dbContext = await dbFactory.CreateDbContextAsync())
        {
            await dbContext.Database.EnsureDeletedAsync();
            await dbContext.Database.EnsureCreatedAsync();
        }

        return new RoundConfigurationTestHarness(dbFactory);
    }

    public async Task<int> CreateTemplateAsync(int totalRounds)
    {
        await using var dbContext = await DbFactory.CreateDbContextAsync();
        var template = BuildTemplate(totalRounds);
        dbContext.GameTemplates.Add(template);
        await dbContext.SaveChangesAsync();
        return template.Id;
    }

    public async Task<int> CreateTemplateWithRoundsAsync(params int[] roundNumbers)
    {
        await using var dbContext = await DbFactory.CreateDbContextAsync();
        var template = BuildTemplate(roundNumbers.Length);
        foreach (var roundNumber in roundNumbers)
        {
            template.Rounds.Add(new RoundTemplate
            {
                RoundNumber = roundNumber,
                RoiResource = ResourceType.Gold,
                Stake = $"Stake {roundNumber}"
            });
        }

        dbContext.GameTemplates.Add(template);
        await dbContext.SaveChangesAsync();
        return template.Id;
    }

    private static GameTemplate BuildTemplate(int totalRounds)
    {
        return new GameTemplate
        {
            Name = $"Test-{Guid.NewGuid():N}",
            GmLoginToken = $"GM{Guid.NewGuid():N}"[..10],
            TotalRounds = totalRounds,
            LocationUpdateIntervalSeconds = 30,
            MinLocationUpdateIntervalSeconds = 5,
            VotingDurationSeconds = 30,
            PredictionBonusPoints = 150,
            VoteTimeoutPenalty = 50,
            DistrictCaptureRadiusMeters = 15d
        };
    }
}

internal sealed class TestDbContextFactory(DbContextOptions<KonqvistDbContext> options) : IDbContextFactory<KonqvistDbContext>
{
    public KonqvistDbContext CreateDbContext() => new(options);

    public Task<KonqvistDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateDbContext());
}
