using Konqvist.Infrastructure.Entities.Enums;
using Konqvist.Infrastructure.Entities.Session;
using Konqvist.Infrastructure.Entities.Template;
using Konqvist.Infrastructure.Persistence;
using Konqvist.Server.Domain.Aggregates;
using Konqvist.Server.Domain.Events;
using Konqvist.Server.Domain.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Konqvist.Server.Tests;

public sealed class GameAggregateTests
{
    [Fact]
    public async Task ClaimDistrict_HappyPath_UpdatesOwnershipAndReturnsDistrictClaimed()
    {
        var fixture = await GameAggregateFixture.CreateAsync();
        var aggregate = fixture.CreateAggregate();

        var result = await aggregate.ClaimDistrict(fixture.RunnerPlayerSessionId, fixture.DistrictSessionId);

        Assert.False(result.WasIdempotent);
        Assert.Equal(nameof(DistrictClaimed), result.PrimaryEvent.EventType);
        Assert.Equal(fixture.TeamOneSessionId, result.PrimaryEvent.TeamSessionId);
        Assert.Equal(fixture.TeamOneSessionId, aggregate.DistrictOwnership[fixture.DistrictSessionId]);

        await using var dbContext = await fixture.DbFactory.CreateDbContextAsync();
        var persistedEvents = await dbContext.GameEvents.OrderBy(entity => entity.Id).ToListAsync();
        Assert.Single(persistedEvents);
        Assert.Equal(nameof(DistrictClaimed), persistedEvents[0].EventType);
    }

    [Fact]
    public async Task ClaimDistrict_DuplicateSameClaim_IsIdempotent()
    {
        var fixture = await GameAggregateFixture.CreateAsync();
        var aggregate = fixture.CreateAggregate();

        var firstResult = await aggregate.ClaimDistrict(fixture.RunnerPlayerSessionId, fixture.DistrictSessionId);
        var rehydratedAggregate = fixture.CreateAggregate();
        var secondResult = await rehydratedAggregate.ClaimDistrict(fixture.RunnerPlayerSessionId, fixture.DistrictSessionId);

        Assert.False(firstResult.WasIdempotent);
        Assert.True(secondResult.WasIdempotent);
        Assert.Equal(firstResult.PrimaryEvent, secondResult.PrimaryEvent);

        await using var dbContext = await fixture.DbFactory.CreateDbContextAsync();
        Assert.Equal(1, await dbContext.GameEvents.CountAsync());
    }

    [Fact]
    public async Task CastVote_HappyPath_ReturnsVoteCastAndTracksVote()
    {
        var fixture = await GameAggregateFixture.CreateAsync();
        var aggregate = fixture.CreateAggregate();
        await aggregate.OpenVoting();

        var result = await aggregate.CastVote(fixture.TeamLeaderPlayerSessionId, fixture.TeamTwoSessionId);

        Assert.False(result.WasIdempotent);
        Assert.Equal(nameof(VoteCast), result.PrimaryEvent.EventType);
        Assert.Equal(fixture.TeamOneSessionId, result.PrimaryEvent.VotingTeamSessionId);
        Assert.Equal(fixture.TeamTwoSessionId, result.PrimaryEvent.TargetTeamSessionId);

        await using var dbContext = await fixture.DbFactory.CreateDbContextAsync();
        var persistedEvents = await dbContext.GameEvents.OrderBy(entity => entity.Id).ToListAsync();
        Assert.Contains(persistedEvents, entity => entity.EventType == nameof(VotingOpened));
        Assert.Contains(persistedEvents, entity => entity.EventType == nameof(GamePhaseChanged));
        Assert.Contains(persistedEvents, entity => entity.EventType == nameof(VoteCast));
    }

    [Fact]
    public async Task CastVote_WhenSameTeamVotesTwice_Rejects()
    {
        var fixture = await GameAggregateFixture.CreateAsync();
        var aggregate = fixture.CreateAggregate();
        await aggregate.OpenVoting();
        await aggregate.CastVote(fixture.TeamLeaderPlayerSessionId, fixture.TeamTwoSessionId);

        var rehydratedAggregate = fixture.CreateAggregate();
        var action = () => rehydratedAggregate.CastVote(fixture.TeamLeaderPlayerSessionId, fixture.TeamTwoSessionId);

        await Assert.ThrowsAsync<InvalidOperationException>(action);

        await using var dbContext = await fixture.DbFactory.CreateDbContextAsync();
        Assert.Equal(3, await dbContext.GameEvents.CountAsync());
        Assert.Equal(1, await dbContext.GameEvents.CountAsync(entity => entity.EventType == nameof(VoteCast)));
    }

    [Fact]
    public async Task ForceLogoutRunner_UsesCurrentDatabaseLoginState_WhenAggregateWasInitializedEarlier()
    {
        var fixture = await GameAggregateFixture.CreateAsync();
        var aggregate = fixture.CreateAggregate();
        await aggregate.ClaimDistrict(fixture.RunnerPlayerSessionId, fixture.DistrictSessionId);

        await using (var dbContext = await fixture.DbFactory.CreateDbContextAsync())
        {
            var targetPlayer = await dbContext.PlayerSessions.SingleAsync(entity => entity.Id == fixture.TeamTwoRunnerPlayerSessionId);
            targetPlayer.IsLoggedIn = true;
            await dbContext.SaveChangesAsync();
        }

        var result = await aggregate.ForceLogoutRunner(fixture.TeamTwoRunnerPlayerSessionId);

        Assert.False(result.WasIdempotent);
        Assert.Equal(nameof(RunnerLogout), result.PrimaryEvent.EventType);

        await using var assertContext = await fixture.DbFactory.CreateDbContextAsync();
        Assert.Equal(1, await assertContext.GameEvents.CountAsync(entity => entity.EventType == nameof(RunnerLogout)));
        Assert.False(await assertContext.PlayerSessions
            .Where(entity => entity.Id == fixture.TeamTwoRunnerPlayerSessionId)
            .Select(entity => entity.IsLoggedIn)
            .SingleAsync());
    }

    private sealed class GameAggregateFixture
    {
        private GameAggregateFixture(
            TestDbContextFactory dbFactory,
            int runnerPlayerSessionId,
            int teamLeaderPlayerSessionId,
            int teamTwoRunnerPlayerSessionId,
            int districtSessionId,
            int teamOneSessionId,
            int teamTwoSessionId)
        {
            DbFactory = dbFactory;
            RunnerPlayerSessionId = runnerPlayerSessionId;
            TeamLeaderPlayerSessionId = teamLeaderPlayerSessionId;
            TeamTwoRunnerPlayerSessionId = teamTwoRunnerPlayerSessionId;
            DistrictSessionId = districtSessionId;
            TeamOneSessionId = teamOneSessionId;
            TeamTwoSessionId = teamTwoSessionId;
        }

        public TestDbContextFactory DbFactory { get; }

        public int RunnerPlayerSessionId { get; }

        public int TeamLeaderPlayerSessionId { get; }

        public int TeamTwoRunnerPlayerSessionId { get; }

        public int DistrictSessionId { get; }

        public int TeamOneSessionId { get; }

        public int TeamTwoSessionId { get; }

        public GameAggregate CreateAggregate()
        {
            var walWriter = new EfGameEventWalWriter(DbFactory);
            return new GameAggregate(DbFactory, walWriter);
        }

        public static async Task<GameAggregateFixture> CreateAsync()
        {
            var dbPath = Path.Combine(Path.GetTempPath(), $"konqvist-game-aggregate-tests-{Guid.NewGuid():N}.db");
            var options = new DbContextOptionsBuilder<KonqvistDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;
            var dbFactory = new TestDbContextFactory(options);

            await using (var dbContext = await dbFactory.CreateDbContextAsync())
            {
                await dbContext.Database.EnsureDeletedAsync();
                await dbContext.Database.EnsureCreatedAsync();

                var gameTemplate = new GameTemplate
                {
                    Name = "Aggregate Template",
                    GmLoginToken = "GM123456",
                    TotalRounds = 2,
                    LocationUpdateIntervalSeconds = 30,
                    MinLocationUpdateIntervalSeconds = 5,
                    VotingDurationSeconds = 60,
                    PredictionBonusPoints = 100,
                    VoteTimeoutPenalty = 50,
                    DistrictCaptureRadiusMeters = 40d
                };

                var teamOneTemplate = new TeamTemplate
                {
                    Name = "Alpha",
                    Color = "#112233"
                };
                teamOneTemplate.Players.Add(new PlayerTemplate
                {
                    LoginToken = "RUNNER01",
                    Role = PlayerRole.Runner
                });
                teamOneTemplate.Players.Add(new PlayerTemplate
                {
                    LoginToken = "LEADER01",
                    Role = PlayerRole.TeamLeader
                });

                var teamTwoTemplate = new TeamTemplate
                {
                    Name = "Beta",
                    Color = "#445566"
                };
                teamTwoTemplate.Players.Add(new PlayerTemplate
                {
                    LoginToken = "RUNNER02",
                    Role = PlayerRole.Runner
                });
                teamTwoTemplate.Players.Add(new PlayerTemplate
                {
                    LoginToken = "LEADER02",
                    Role = PlayerRole.TeamLeader
                });

                gameTemplate.Teams.Add(teamOneTemplate);
                gameTemplate.Teams.Add(teamTwoTemplate);
                gameTemplate.Districts.Add(new DistrictTemplate
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
                gameTemplate.Rounds.Add(new RoundTemplate
                {
                    RoundNumber = 1,
                    RoiResource = ResourceType.Gold,
                    Stake = "First round"
                });
                gameTemplate.Rounds.Add(new RoundTemplate
                {
                    RoundNumber = 2,
                    RoiResource = ResourceType.Likes,
                    Stake = "Second round"
                });

                dbContext.GameTemplates.Add(gameTemplate);
                await dbContext.SaveChangesAsync();

                var session = new GameSession
                {
                    GameTemplateId = gameTemplate.Id,
                    Status = GameStatus.Running,
                    CurrentPhase = GamePhase.Gathering
                };

                var teamOneSession = new TeamSession
                {
                    GameSession = session,
                    TeamTemplateId = teamOneTemplate.Id
                };
                var teamTwoSession = new TeamSession
                {
                    GameSession = session,
                    TeamTemplateId = teamTwoTemplate.Id
                };
                session.Teams.Add(teamOneSession);
                session.Teams.Add(teamTwoSession);

                var runnerSession = new PlayerSession
                {
                    GameSession = session,
                    PlayerTemplateId = teamOneTemplate.Players.Single(entity => entity.Role == PlayerRole.Runner).Id,
                    IsLoggedIn = true
                };
                var leaderSession = new PlayerSession
                {
                    GameSession = session,
                    PlayerTemplateId = teamOneTemplate.Players.Single(entity => entity.Role == PlayerRole.TeamLeader).Id,
                    IsLoggedIn = true
                };
                session.Players.Add(runnerSession);
                session.Players.Add(leaderSession);
                session.Players.Add(new PlayerSession
                {
                    GameSession = session,
                    PlayerTemplateId = teamTwoTemplate.Players.Single(entity => entity.Role == PlayerRole.Runner).Id
                });
                session.Players.Add(new PlayerSession
                {
                    GameSession = session,
                    PlayerTemplateId = teamTwoTemplate.Players.Single(entity => entity.Role == PlayerRole.TeamLeader).Id
                });

                var districtSession = new DistrictSession
                {
                    GameSession = session,
                    DistrictTemplateId = gameTemplate.Districts.Single().Id
                };
                session.Districts.Add(districtSession);

                var roundOneSession = new RoundSession
                {
                    GameSession = session,
                    RoundTemplateId = gameTemplate.Rounds.Single(entity => entity.RoundNumber == 1).Id,
                    Status = RoundStatus.Gathering
                };
                var roundTwoSession = new RoundSession
                {
                    GameSession = session,
                    RoundTemplateId = gameTemplate.Rounds.Single(entity => entity.RoundNumber == 2).Id,
                    Status = RoundStatus.Gathering
                };
                session.Rounds.Add(roundOneSession);
                session.Rounds.Add(roundTwoSession);

                dbContext.GameSessions.Add(session);
                await dbContext.SaveChangesAsync();

                session.CurrentRoundSessionId = roundOneSession.Id;
                await dbContext.SaveChangesAsync();

                return new GameAggregateFixture(
                    dbFactory,
                    runnerSession.Id,
                    leaderSession.Id,
                    session.Players.Single(entity => entity.PlayerTemplateId == teamTwoTemplate.Players.Single(player => player.Role == PlayerRole.Runner).Id).Id,
                    districtSession.Id,
                    teamOneSession.Id,
                    teamTwoSession.Id);
            }
        }
    }
}
