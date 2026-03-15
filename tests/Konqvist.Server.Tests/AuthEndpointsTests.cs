using System.Net;
using System.Net.Http.Json;
using Konqvist.Infrastructure.Entities.Enums;
using Konqvist.Infrastructure.Entities.Session;
using Konqvist.Infrastructure.Entities.Template;
using Konqvist.Infrastructure.Persistence;
using Konqvist.Server.Features.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Konqvist.Server.Tests;

[Collection(ServerAppFactoryCollection.Name)]
public sealed class AuthEndpointsTests
{
    [Fact]
    public async Task Login_WithValidToken_ReturnsIdentityAndMeReturnsCurrentPlayer()
    {
        await using var factory = new ServerAppFactory();
        await CreatePendingSessionFromSeedTemplateAsync(factory.Services);
        var client = CreateHttpsClient(factory);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { token = "AR15ee" });
        var meResponse = await client.GetAsync("/api/auth/me");
        var meBody = await meResponse.Content.ReadFromJsonAsync<AuthIdentityResponse>();

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        Assert.NotNull(meBody);
        Assert.Equal("Runner", meBody.Role);
        Assert.Equal("Alpha", meBody.Team);
        Assert.True(meBody.PlayerSessionId > 0);
        Assert.Equal(GameStatus.Pending.ToString(), meBody.GameStatus);
        Assert.Equal(GamePhase.WaitingForPlayers.ToString(), meBody.GamePhase);
    }

    [Fact]
    public async Task Login_WithValidGameMasterToken_ReturnsIdentityAndMeReturnsGameMaster()
    {
        await using var factory = new ServerAppFactory();
        await CreatePendingSessionFromSeedTemplateAsync(factory.Services);
        var client = CreateHttpsClient(factory);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { token = "GM57t7" });
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<AuthIdentityResponse>();
        var meResponse = await client.GetAsync("/api/auth/me");
        var meBody = await meResponse.Content.ReadFromJsonAsync<AuthIdentityResponse>();

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.NotNull(loginBody);
        Assert.Equal("GameMaster", loginBody.Role);
        Assert.Equal(string.Empty, loginBody.Team);
        Assert.Null(loginBody.PlayerSessionId);

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        Assert.NotNull(meBody);
        Assert.Equal("GameMaster", meBody.Role);
        Assert.Equal(string.Empty, meBody.Team);
        Assert.Null(meBody.PlayerSessionId);
        Assert.Equal(GameStatus.Pending.ToString(), meBody.GameStatus);
        Assert.Equal(GamePhase.WaitingForPlayers.ToString(), meBody.GamePhase);
    }

    [Fact]
    public async Task Login_WhenAnotherRunnerLoggedInForTeam_ReturnsConflict()
    {
        await using var factory = new ServerAppFactory();
        var sessionId = await CreatePendingSessionFromSeedTemplateAsync(factory.Services);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<KonqvistDbContext>();
            var alphaTeamId = await dbContext.TeamTemplates
                .Where(entity => entity.Name == "Alpha")
                .Select(entity => entity.Id)
                .SingleAsync();

            var extraRunner = new PlayerTemplate
            {
                TeamTemplateId = alphaTeamId,
                LoginToken = $"AR{Guid.NewGuid():N}"[..10],
                Role = PlayerRole.Runner
            };
            dbContext.PlayerTemplates.Add(extraRunner);
            await dbContext.SaveChangesAsync();

            dbContext.PlayerSessions.Add(new PlayerSession
            {
                GameSessionId = sessionId,
                PlayerTemplateId = extraRunner.Id,
                IsLoggedIn = true
            });
            await dbContext.SaveChangesAsync();
        }

        var client = CreateHttpsClient(factory);

        var response = await client.PostAsJsonAsync("/api/auth/login", new { token = "AR15ee" });
        var body = await response.Content.ReadFromJsonAsync<AuthErrorResponse>();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.NotNull(body);
        Assert.Contains("Runner slot for team 'Alpha' is already in use.", body.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Logout_SetsPlayerSessionLoggedOut_AndMeReturnsUnauthorized()
    {
        await using var factory = new ServerAppFactory();
        await CreatePendingSessionFromSeedTemplateAsync(factory.Services);
        var client = CreateHttpsClient(factory);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { token = "AR15ee" });
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<AuthIdentityResponse>();
        var logoutResponse = await client.PostAsync("/api/auth/logout", null);
        var meResponse = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.NotNull(loginBody);
        Assert.True(loginBody.PlayerSessionId.HasValue);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KonqvistDbContext>();
        var alphaRunnerSession = await dbContext.PlayerSessions
            .SingleAsync(entity => entity.Id == loginBody.PlayerSessionId!.Value);
        Assert.False(alphaRunnerSession.IsLoggedIn);
    }

    [Fact]
    public async Task Me_WithoutCookie_ReturnsUnauthorized()
    {
        await using var factory = new ServerAppFactory();
        await CreatePendingSessionFromSeedTemplateAsync(factory.Services);
        var client = CreateHttpsClient(factory);

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TeamStatus_WhenRunnerLoggedIn_ReturnsTokensAndRunnerSlotTaken()
    {
        await using var factory = new ServerAppFactory();
        var sessionId = await CreatePendingSessionFromSeedTemplateAsync(factory.Services);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<KonqvistDbContext>();
            var alphaRunnerTemplateId = await dbContext.PlayerTemplates
                .Where(entity => entity.LoginToken == "AR15ee")
                .Select(entity => entity.Id)
                .SingleAsync();
            var alphaRunnerSession = await dbContext.PlayerSessions
                .SingleAsync(entity => entity.GameSessionId == sessionId && entity.PlayerTemplateId == alphaRunnerTemplateId);
            alphaRunnerSession.IsLoggedIn = true;
            await dbContext.SaveChangesAsync();
        }

        var client = CreateHttpsClient(factory);

        var response = await client.GetAsync("/api/auth/team-status/Alpha");
        var body = await response.Content.ReadFromJsonAsync<TeamStatusResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("Alpha", body.TeamName);
        Assert.True(body.RunnerSlotTaken);
        Assert.Equal("AR15ee", body.RunnerToken);
        Assert.Equal("ATC5y85", body.TeamCaptainToken);
    }

    private static async Task<int> CreatePendingSessionFromSeedTemplateAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KonqvistDbContext>();

        var template = await dbContext.GameTemplates
            .AsNoTracking()
            .Include(entity => entity.Teams)
            .ThenInclude(entity => entity.Players)
            .OrderBy(entity => entity.Id)
            .FirstAsync();

        var session = new GameSession
        {
            GameTemplateId = template.Id,
            Status = GameStatus.Pending,
            CurrentPhase = GamePhase.WaitingForPlayers
        };

        foreach (var team in template.Teams.OrderBy(entity => entity.Id))
        {
            session.Teams.Add(new TeamSession
            {
                TeamTemplateId = team.Id
            });

            foreach (var player in team.Players.OrderBy(entity => entity.Id))
            {
                session.Players.Add(new PlayerSession
                {
                    PlayerTemplateId = player.Id
                });
            }
        }

        dbContext.GameSessions.Add(session);
        await dbContext.SaveChangesAsync();
        return session.Id;
    }

    private static HttpClient CreateHttpsClient(WebApplicationFactory<ServerEntryPointMarker> factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    private sealed class ServerAppFactory : WebApplicationFactory<ServerEntryPointMarker>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"konqvist-auth-tests-{Guid.NewGuid():N}.db");

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
