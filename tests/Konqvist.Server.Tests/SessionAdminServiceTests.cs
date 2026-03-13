using Konqvist.Admin.Features.Session;
using Konqvist.Admin.Features.Rounds;
using Konqvist.Admin.Features.Templates;
using Konqvist.Infrastructure.Entities.Enums;
using Konqvist.Infrastructure.Entities.Template;
using Microsoft.EntityFrameworkCore;

namespace Konqvist.Server.Tests;

public sealed class SessionAdminServiceTests
{
    [Fact]
    public async Task CreateAsync_CreatesPendingSessionFromTemplate()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await CreateTemplateWithDataAsync(harness.TemplateService, harness.DbFactory);
        var sessionService = new SessionAdminService(harness.DbFactory);

        var result = await sessionService.CreateAsync(templateId);

        Assert.Equal(CreateGameSessionResult.Created, result);

        await using var dbContext = await harness.DbFactory.CreateDbContextAsync();
        var session = await dbContext.GameSessions
            .Include(entity => entity.Teams)
            .Include(entity => entity.Players)
            .Include(entity => entity.Districts)
            .Include(entity => entity.Rounds)
            .SingleAsync();

        Assert.Equal(GameStatus.Pending, session.Status);
        Assert.Equal(GamePhase.WaitingForPlayers, session.CurrentPhase);
        Assert.Equal(1, session.Teams.Count);
        Assert.Equal(2, session.Players.Count);
        Assert.Equal(1, session.Districts.Count);
        Assert.Equal(RoundTemplateDefaults.DefaultRoundCount, session.Rounds.Count);
    }

    [Fact]
    public async Task CreateAsync_BlocksWhenPendingOrRunningSessionExists()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var firstTemplateId = await CreateTemplateWithDataAsync(harness.TemplateService, harness.DbFactory);
        var secondTemplateId = await CreateTemplateWithDataAsync(harness.TemplateService, harness.DbFactory);
        var sessionService = new SessionAdminService(harness.DbFactory);
        await sessionService.CreateAsync(firstTemplateId);

        var secondCreateResult = await sessionService.CreateAsync(secondTemplateId);

        Assert.Equal(CreateGameSessionResult.ActiveSessionExists, secondCreateResult);
    }

    [Fact]
    public async Task StartAsync_FromPending_SetsRunningAndStartedAt()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await CreateTemplateWithDataAsync(harness.TemplateService, harness.DbFactory);
        var sessionService = new SessionAdminService(harness.DbFactory);
        await sessionService.CreateAsync(templateId);

        await using (var dbContext = await harness.DbFactory.CreateDbContextAsync())
        {
            var pendingSessionId = await dbContext.GameSessions.Select(entity => entity.Id).SingleAsync();
            var startResult = await sessionService.StartAsync(pendingSessionId);
            Assert.Equal(StartGameSessionResult.Started, startResult);
        }

        await using var assertContext = await harness.DbFactory.CreateDbContextAsync();
        var session = await assertContext.GameSessions.SingleAsync();
        Assert.Equal(GameStatus.Running, session.Status);
        Assert.NotNull(session.StartedAt);
    }

    [Fact]
    public async Task ResetAsync_FromFinished_RemovesSessionLayerAndRecreatesPendingSession()
    {
        var harness = await RoundConfigurationTestHarness.CreateAsync();
        var templateId = await CreateTemplateWithDataAsync(harness.TemplateService, harness.DbFactory);
        var sessionService = new SessionAdminService(harness.DbFactory);
        await sessionService.CreateAsync(templateId);

        await using (var dbContext = await harness.DbFactory.CreateDbContextAsync())
        {
            var firstSession = await dbContext.GameSessions.SingleAsync();
            firstSession.Status = GameStatus.Finished;
            firstSession.FinishedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();
        }

        await sessionService.CreateAsync(templateId);

        var resetTargetId = 0;
        await using (var dbContext = await harness.DbFactory.CreateDbContextAsync())
        {
            var pendingSession = await dbContext.GameSessions.SingleAsync(entity => entity.Status == GameStatus.Pending);
            pendingSession.Status = GameStatus.Finished;
            pendingSession.FinishedAt = DateTime.UtcNow;
            resetTargetId = pendingSession.Id;
            await dbContext.SaveChangesAsync();
        }

        var resetResult = await sessionService.ResetAsync(resetTargetId);

        Assert.Equal(ResetGameSessionResult.Reset, resetResult);

        await using var assertContext = await harness.DbFactory.CreateDbContextAsync();
        Assert.Equal(1, await assertContext.GameSessions.CountAsync());
        Assert.Equal(1, await assertContext.TeamSessions.CountAsync());
        Assert.Equal(2, await assertContext.PlayerSessions.CountAsync());
        Assert.Equal(1, await assertContext.DistrictSessions.CountAsync());
        Assert.Equal(RoundTemplateDefaults.DefaultRoundCount, await assertContext.RoundSessions.CountAsync());
        Assert.Equal(0, await assertContext.Votes.CountAsync());
        Assert.Equal(0, await assertContext.GameEvents.CountAsync());
        Assert.Equal(0, await assertContext.RoundSnapshots.CountAsync());
        Assert.Equal(0, await assertContext.DistrictOwnershipSnapshots.CountAsync());

        var recreatedSession = await assertContext.GameSessions.SingleAsync();
        Assert.Equal(GameStatus.Pending, recreatedSession.Status);
        Assert.Equal(GamePhase.WaitingForPlayers, recreatedSession.CurrentPhase);
        Assert.Null(recreatedSession.StartedAt);
    }

    private static async Task<int> CreateTemplateWithDataAsync(
        GameTemplateAdminService templateService,
        TestDbContextFactory dbFactory)
    {
        var templateId = await templateService.CreateAsync(new CreateGameTemplateInput
        {
            Name = $"Session Template {Guid.NewGuid():N}"[..24],
            LocationUpdateIntervalSeconds = 30,
            MinLocationUpdateIntervalSeconds = 5,
            VotingDurationSeconds = 30,
            PredictionBonusPoints = 100,
            VoteTimeoutPenalty = 50,
            DistrictCaptureRadiusMeters = 40d
        });

        await using var dbContext = await dbFactory.CreateDbContextAsync();
        var template = await dbContext.GameTemplates
            .Include(entity => entity.Teams)
            .ThenInclude(entity => entity.Players)
            .Include(entity => entity.Districts)
            .SingleAsync(entity => entity.Id == templateId);

        var team = new TeamTemplate
        {
            GameTemplateId = templateId,
            Name = "Team One",
            Color = "#112233",
            Players =
            [
                new PlayerTemplate { LoginToken = $"RUN{Guid.NewGuid():N}"[..11], Role = PlayerRole.Runner },
                new PlayerTemplate { LoginToken = $"CAP{Guid.NewGuid():N}"[..11], Role = PlayerRole.TeamLeader }
            ]
        };
        template.Teams.Add(team);
        template.Districts.Add(new DistrictTemplate
        {
            Name = "District One",
            GeoJson = "{\"type\":\"Polygon\",\"coordinates\":[]}",
            TriggerLat = 52.0d,
            TriggerLng = 5.0d,
            TriggerRadiusMeters = 100d,
            Gold = 10,
            Voters = 20,
            Likes = 30,
            Oil = 40
        });

        await dbContext.SaveChangesAsync();
        return templateId;
    }
}
