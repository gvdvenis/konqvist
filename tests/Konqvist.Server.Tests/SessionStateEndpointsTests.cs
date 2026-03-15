using System.Net;
using System.Net.Http.Json;
using Konqvist.Infrastructure.Entities.Enums;
using Konqvist.Infrastructure.Entities.Session;
using Konqvist.Infrastructure.Entities.Template;
using Konqvist.Infrastructure.Persistence;
using Konqvist.Server.Features.SessionState;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Konqvist.Server.Tests;

[Collection(ServerAppFactoryCollection.Name)]
public sealed class SessionStateEndpointsTests
{
    [Fact]
    public async Task GetState_WithAuthenticatedPlayer_ReturnsFullSessionSnapshot()
    {
        await using var factory = new ServerAppFactory();
        var scenario = await SeedSessionScenarioAsync(factory.Services, GamePhase.Voting, votingEnabled: true);
        var client = CreateHttpsClient(factory);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { token = scenario.RunnerToken });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<KonqvistDbContext>();

            var alphaTeam = await dbContext.TeamSessions.SingleAsync(entity => entity.Id == scenario.TeamSessionId);
            alphaTeam.TotalScore = 125;
            alphaTeam.TotalGold = 11;
            alphaTeam.TotalVoters = 22;
            alphaTeam.TotalLikes = 33;
            alphaTeam.TotalOil = 44;

            var betaTeam = await dbContext.TeamSessions.SingleAsync(entity => entity.Id == scenario.OtherTeamSessionId);
            betaTeam.TotalScore = 210;
            betaTeam.TotalGold = 55;
            betaTeam.TotalVoters = 66;
            betaTeam.TotalLikes = 77;
            betaTeam.TotalOil = 88;

            var district = await dbContext.DistrictSessions.SingleAsync(entity => entity.Id == scenario.DistrictSessionId);
            district.CurrentOwnerTeamSessionId = scenario.OtherTeamSessionId;

            var runnerSession = await dbContext.PlayerSessions.SingleAsync(entity => entity.Id == scenario.RunnerPlayerSessionId);
            runnerSession.IsOnline = true;
            runnerSession.LocationLat = 52.1234d;
            runnerSession.LocationLng = 5.4321d;
            runnerSession.LocationUpdatedAt = DateTime.UtcNow;

            var otherRunnerSession = await dbContext.PlayerSessions.SingleAsync(entity => entity.Id == scenario.OtherRunnerPlayerSessionId);
            otherRunnerSession.IsLoggedIn = true;
            otherRunnerSession.IsOnline = true;
            otherRunnerSession.LocationLat = 53.1234d;
            otherRunnerSession.LocationLng = 6.4321d;
            otherRunnerSession.LocationUpdatedAt = DateTime.UtcNow;

            dbContext.Votes.Add(new Vote
            {
                RoundSessionId = scenario.RoundSessionId,
                VotingTeamSessionId = scenario.TeamSessionId,
                TargetTeamSessionId = scenario.OtherTeamSessionId,
                VoteValue = 7,
                CastAt = DateTime.UtcNow
            });

            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/session/state");
        var body = await response.Content.ReadFromJsonAsync<SessionStateResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(GamePhase.Voting, body.Game.CurrentPhase);
        Assert.Equal(1, body.Game.CurrentRoundNumber);
        Assert.Equal(scenario.GameSessionId, body.Game.GameSessionId);
        Assert.True(body.Map.DistrictOwners.TryGetValue(scenario.DistrictSessionId, out var currentOwnerTeamSessionId));
        Assert.Equal(scenario.OtherTeamSessionId, currentOwnerTeamSessionId);
        Assert.True(body.Map.RunnerPositions.TryGetValue(scenario.RunnerPlayerSessionId, out var runnerPosition));
        Assert.Equal(52.1234d, runnerPosition.Latitude);
        Assert.Equal(5.4321d, runnerPosition.Longitude);
        Assert.DoesNotContain(scenario.OtherRunnerPlayerSessionId, body.Map.RunnerPositions.Keys);
        Assert.True(body.Voting.IsVotingOpen);
        Assert.NotNull(body.Voting.TimeRemaining);
        Assert.True(body.Voting.VotesPerTeam.TryGetValue(scenario.OtherTeamSessionId, out var voteTotal));
        Assert.Equal(7, voteTotal);
        Assert.Equal(125, body.Scores.TeamScores[scenario.TeamSessionId]);
        Assert.Equal(210, body.Scores.TeamScores[scenario.OtherTeamSessionId]);
        Assert.Equal(11, body.Scores.TeamResources[scenario.TeamSessionId].Gold);
        Assert.Equal(66, body.Scores.TeamResources[scenario.OtherTeamSessionId].Voters);
        Assert.Equal(scenario.RunnerPlayerSessionId, body.Player.PlayerSessionId);
        Assert.Equal(scenario.TeamSessionId, body.Player.TeamSessionId);
        Assert.Equal(scenario.TeamName, body.Player.TeamName);
        Assert.Equal(PlayerRole.Runner, body.Player.Role);
        Assert.True(body.Player.IsLoggedIn);
        Assert.True(body.Player.IsOnline);
    }

    [Fact]
    public async Task GetState_WithoutCookie_ReturnsUnauthorized()
    {
        await using var factory = new ServerAppFactory();
        await SeedSessionScenarioAsync(factory.Services, GamePhase.Gathering);
        var client = CreateHttpsClient(factory);

        var response = await client.GetAsync("/api/session/state");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetState_WithAuthenticatedGameMaster_ReturnsFullSessionSnapshotForAllTeams()
    {
        await using var factory = new ServerAppFactory();
        var scenario = await SeedSessionScenarioAsync(factory.Services, GamePhase.Voting, votingEnabled: true);
        var client = CreateHttpsClient(factory);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { token = scenario.GmToken });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<KonqvistDbContext>();

            var runnerSession = await dbContext.PlayerSessions.SingleAsync(entity => entity.Id == scenario.RunnerPlayerSessionId);
            runnerSession.IsLoggedIn = true;
            runnerSession.IsOnline = true;
            runnerSession.LocationLat = 52.1234d;
            runnerSession.LocationLng = 5.4321d;
            runnerSession.LocationUpdatedAt = DateTime.UtcNow;

            var otherRunnerSession = await dbContext.PlayerSessions.SingleAsync(entity => entity.Id == scenario.OtherRunnerPlayerSessionId);
            otherRunnerSession.IsLoggedIn = true;
            otherRunnerSession.IsOnline = true;
            otherRunnerSession.LocationLat = 53.1234d;
            otherRunnerSession.LocationLng = 6.4321d;
            otherRunnerSession.LocationUpdatedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/session/state");
        var body = await response.Content.ReadFromJsonAsync<SessionStateResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(GamePhase.Voting, body.Game.CurrentPhase);
        Assert.Equal(scenario.GameSessionId, body.Game.GameSessionId);
        Assert.True(body.Map.RunnerPositions.ContainsKey(scenario.RunnerPlayerSessionId));
        Assert.True(body.Map.RunnerPositions.ContainsKey(scenario.OtherRunnerPlayerSessionId));
        Assert.Null(body.Player.PlayerSessionId);
        Assert.Null(body.Player.TeamSessionId);
        Assert.Null(body.Player.TeamName);
        Assert.Equal(PlayerRole.GameMaster, body.Player.Role);
        Assert.True(body.Player.IsLoggedIn);
        Assert.False(body.Player.IsOnline);
    }

    private static async Task<SessionScenario> SeedSessionScenarioAsync(
        IServiceProvider services,
        GamePhase phase,
        bool votingEnabled = false)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KonqvistDbContext>();

        var gameTemplate = new GameTemplate
        {
            Name = $"Session Template {Guid.NewGuid():N}",
            GmLoginToken = $"GM{Guid.NewGuid():N}"[..10],
            TotalRounds = 1,
            LocationUpdateIntervalSeconds = 30,
            MinLocationUpdateIntervalSeconds = 5,
            VotingDurationSeconds = 60,
            PredictionBonusPoints = 100,
            VoteTimeoutPenalty = 25,
            DistrictCaptureRadiusMeters = 40d
        };

        var runnerToken = $"SR{Guid.NewGuid():N}"[..10];
        var teamLeaderToken = $"ST{Guid.NewGuid():N}"[..10];
        var otherRunnerToken = $"TR{Guid.NewGuid():N}"[..10];
        var otherTeamLeaderToken = $"TT{Guid.NewGuid():N}"[..10];

        var alphaTeam = new TeamTemplate
        {
            Name = $"StateAlpha{Guid.NewGuid():N}"[..14],
            Color = "#112233"
        };
        alphaTeam.Players.Add(new PlayerTemplate
        {
            LoginToken = runnerToken,
            Role = PlayerRole.Runner
        });
        alphaTeam.Players.Add(new PlayerTemplate
        {
            LoginToken = teamLeaderToken,
            Role = PlayerRole.TeamLeader
        });

        var betaTeam = new TeamTemplate
        {
            Name = $"StateBeta{Guid.NewGuid():N}"[..13],
            Color = "#445566"
        };
        betaTeam.Players.Add(new PlayerTemplate
        {
            LoginToken = otherRunnerToken,
            Role = PlayerRole.Runner
        });
        betaTeam.Players.Add(new PlayerTemplate
        {
            LoginToken = otherTeamLeaderToken,
            Role = PlayerRole.TeamLeader
        });

        gameTemplate.Teams.Add(alphaTeam);
        gameTemplate.Teams.Add(betaTeam);
        gameTemplate.Districts.Add(new DistrictTemplate
        {
            Name = "Session District",
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
            Stake = "Session state stake"
        });

        dbContext.GameTemplates.Add(gameTemplate);
        await dbContext.SaveChangesAsync();

        var session = new GameSession
        {
            GameTemplateId = gameTemplate.Id,
            Status = GameStatus.Running,
            CurrentPhase = phase
        };

        var alphaTeamSession = new TeamSession
        {
            GameSession = session,
            TeamTemplateId = alphaTeam.Id
        };
        var betaTeamSession = new TeamSession
        {
            GameSession = session,
            TeamTemplateId = betaTeam.Id
        };

        var runnerPlayerSession = new PlayerSession
        {
            GameSession = session,
            PlayerTemplateId = alphaTeam.Players.Single(entity => entity.Role == PlayerRole.Runner).Id,
            IsLoggedIn = false
        };
        var leaderPlayerSession = new PlayerSession
        {
            GameSession = session,
            PlayerTemplateId = alphaTeam.Players.Single(entity => entity.Role == PlayerRole.TeamLeader).Id,
            IsLoggedIn = false
        };
        var otherRunnerPlayerSession = new PlayerSession
        {
            GameSession = session,
            PlayerTemplateId = betaTeam.Players.Single(entity => entity.Role == PlayerRole.Runner).Id,
            IsLoggedIn = false
        };
        var otherLeaderPlayerSession = new PlayerSession
        {
            GameSession = session,
            PlayerTemplateId = betaTeam.Players.Single(entity => entity.Role == PlayerRole.TeamLeader).Id,
            IsLoggedIn = false
        };

        var districtSession = new DistrictSession
        {
            GameSession = session,
            DistrictTemplateId = gameTemplate.Districts.Single().Id
        };
        var roundSession = new RoundSession
        {
            GameSession = session,
            RoundTemplateId = gameTemplate.Rounds.Single().Id,
            Status = phase == GamePhase.Voting ? RoundStatus.Voting : RoundStatus.Gathering,
            VotingEnabled = votingEnabled,
            VotingStartedAt = votingEnabled ? DateTime.UtcNow : null
        };

        session.Teams.Add(alphaTeamSession);
        session.Teams.Add(betaTeamSession);
        session.Players.Add(runnerPlayerSession);
        session.Players.Add(leaderPlayerSession);
        session.Players.Add(otherRunnerPlayerSession);
        session.Players.Add(otherLeaderPlayerSession);
        session.Districts.Add(districtSession);
        session.Rounds.Add(roundSession);

        dbContext.GameSessions.Add(session);
        await dbContext.SaveChangesAsync();

        session.CurrentRoundSessionId = roundSession.Id;
        await dbContext.SaveChangesAsync();

        return new SessionScenario(
            session.Id,
            alphaTeamSession.Id,
            betaTeamSession.Id,
            districtSession.Id,
            roundSession.Id,
            runnerPlayerSession.Id,
            otherRunnerPlayerSession.Id,
            alphaTeam.Name,
            runnerToken,
            gameTemplate.GmLoginToken);
    }

    private static HttpClient CreateHttpsClient(WebApplicationFactory<ServerEntryPointMarker> factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    private sealed record SessionScenario(
        int GameSessionId,
        int TeamSessionId,
        int OtherTeamSessionId,
        int DistrictSessionId,
        int RoundSessionId,
        int RunnerPlayerSessionId,
        int OtherRunnerPlayerSessionId,
        string TeamName,
        string RunnerToken,
        string GmToken);

    private sealed class ServerAppFactory : WebApplicationFactory<ServerEntryPointMarker>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"konqvist-sessionstate-tests-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_dbPath}");
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = $"Data Source={_dbPath}"
                });
            });
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            try
            {
                if (File.Exists(_dbPath))
                {
                    File.Delete(_dbPath);
                }
            }
            catch (IOException)
            {
            }
        }
    }
}
