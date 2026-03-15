using Konqvist.Infrastructure.Entities.Enums;
using Konqvist.Infrastructure.Entities.Session;
using Konqvist.Infrastructure.Entities.Template;
using Konqvist.Infrastructure.Persistence;
using Konqvist.Server.Domain.Events;
using Konqvist.Server.Domain.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Konqvist.Server.Tests;

public sealed class GameEventRepositoryTests
{
    [Fact]
    public async Task AppendAsync_PersistsRunnerLoginEvent()
    {
        var fixture = await GameEventRepositoryFixture.CreateAsync();
        var repository = new GameEventRepository(fixture.DbFactory);

        await repository.AppendAsync(
        [
            new RunnerLogin(fixture.GameSessionId, fixture.PlayerSessionId, DateTime.UtcNow)
        ]);

        await using var dbContext = await fixture.DbFactory.CreateDbContextAsync();
        var persistedEvent = await dbContext.GameEvents.SingleAsync();

        Assert.Equal(nameof(RunnerLogin), persistedEvent.EventType);
        Assert.Equal(fixture.GameSessionId, persistedEvent.GameSessionId);
        Assert.Equal(fixture.PlayerSessionId, persistedEvent.ActorPlayerSessionId);
    }

    [Fact]
    public async Task AppendAsync_SkipsUnsupportedEventsSilently()
    {
        var fixture = await GameEventRepositoryFixture.CreateAsync();
        var repository = new GameEventRepository(fixture.DbFactory);

        await repository.AppendAsync(
        [
            new UnsupportedGameEvent(fixture.GameSessionId, DateTime.UtcNow)
        ]);

        await using var dbContext = await fixture.DbFactory.CreateDbContextAsync();
        Assert.Equal(0, await dbContext.GameEvents.CountAsync());
    }

    private sealed record UnsupportedGameEvent(
        int GameSessionId,
        DateTime OccurredAt) : IGameDomainEvent
    {
        public string EventType => nameof(UnsupportedGameEvent);

        public int? RoundSessionId => null;

        public int? ActorPlayerSessionId => null;
    }

    private sealed class GameEventRepositoryFixture(TestDbContextFactory dbFactory, int gameSessionId, int playerSessionId)
    {
        public TestDbContextFactory DbFactory { get; } = dbFactory;

        public int GameSessionId { get; } = gameSessionId;

        public int PlayerSessionId { get; } = playerSessionId;

        public static async Task<GameEventRepositoryFixture> CreateAsync()
        {
            var dbPath = Path.Combine(Path.GetTempPath(), $"konqvist-game-event-repo-tests-{Guid.NewGuid():N}.db");
            var options = new DbContextOptionsBuilder<KonqvistDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;
            var dbFactory = new TestDbContextFactory(options);

            await using var dbContext = await dbFactory.CreateDbContextAsync();
            await dbContext.Database.EnsureDeletedAsync();
            await dbContext.Database.EnsureCreatedAsync();

            var gameTemplate = new GameTemplate
            {
                Name = "Repository Template",
                GmLoginToken = "GM654321",
                TotalRounds = 1,
                LocationUpdateIntervalSeconds = 30,
                MinLocationUpdateIntervalSeconds = 5,
                VotingDurationSeconds = 60,
                PredictionBonusPoints = 100,
                VoteTimeoutPenalty = 50,
                DistrictCaptureRadiusMeters = 40d
            };

            var teamTemplate = new TeamTemplate
            {
                Name = "Alpha",
                Color = "#112233"
            };
            teamTemplate.Players.Add(new PlayerTemplate
            {
                LoginToken = "RUNNER01",
                Role = PlayerRole.Runner
            });

            gameTemplate.Teams.Add(teamTemplate);
            dbContext.GameTemplates.Add(gameTemplate);
            await dbContext.SaveChangesAsync();

            var gameSession = new GameSession
            {
                GameTemplateId = gameTemplate.Id,
                Status = GameStatus.Running,
                CurrentPhase = GamePhase.Gathering
            };

            var playerSession = new PlayerSession
            {
                GameSession = gameSession,
                PlayerTemplateId = teamTemplate.Players.Single().Id,
                IsLoggedIn = true
            };

            gameSession.Players.Add(playerSession);
            dbContext.GameSessions.Add(gameSession);
            await dbContext.SaveChangesAsync();

            return new GameEventRepositoryFixture(dbFactory, gameSession.Id, playerSession.Id);
        }
    }
}
