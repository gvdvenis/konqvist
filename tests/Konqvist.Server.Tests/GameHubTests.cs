using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Konqvist.Infrastructure.Entities.Enums;
using Konqvist.Infrastructure.Entities.Session;
using Konqvist.Infrastructure.Entities.Template;
using Konqvist.Infrastructure.Persistence;
using Konqvist.Server.Features.Auth;
using Konqvist.Server.Hubs;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Konqvist.Server.Tests;

[Collection(ServerAppFactoryCollection.Name)]
public sealed class GameHubTests
{
    [Fact]
    public async Task StartGameEndpoint_WithGameMasterCookie_TransitionsPendingSession_AndBroadcastsPhaseChanged()
    {
        await using var factory = new ServerAppFactory();
        var scenario = await SeedHubScenarioAsync(factory.Services, GamePhase.WaitingForPlayers, status: GameStatus.Pending);

        var runnerCookie = await LoginAndGetCookieAsync(factory, scenario.RunnerToken);
        await using var runnerConnection = CreateHubConnection(factory, runnerCookie);
        var runnerPhaseChanged = new TaskCompletionSource<PhaseChangedMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        runnerConnection.On<PhaseChangedMessage>(nameof(IGameClient.PhaseChanged), message =>
        {
            if (message.GameSessionId == scenario.GameSessionId)
            {
                runnerPhaseChanged.TrySetResult(message);
            }
        });
        await runnerConnection.StartAsync();

        var leaderCookie = await LoginAndGetCookieAsync(factory, scenario.TeamLeaderToken);
        await using var leaderConnection = CreateHubConnection(factory, leaderCookie);
        var leaderPhaseChanged = new TaskCompletionSource<PhaseChangedMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        leaderConnection.On<PhaseChangedMessage>(nameof(IGameClient.PhaseChanged), message =>
        {
            if (message.GameSessionId == scenario.GameSessionId)
            {
                leaderPhaseChanged.TrySetResult(message);
            }
        });
        await leaderConnection.StartAsync();

        var gameMasterCookie = await LoginAndGetCookieAsync(factory, scenario.GmToken);
        var client = CreateHttpsClient(factory);
        client.DefaultRequestHeaders.Add("Cookie", gameMasterCookie);

        var response = await client.PostAsync("/api/game/start", content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var runnerMessage = await runnerPhaseChanged.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var leaderMessage = await leaderPhaseChanged.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(GamePhase.WaitingForPlayers.ToString(), runnerMessage.PreviousPhase);
        Assert.Equal(GamePhase.Gathering.ToString(), runnerMessage.CurrentPhase);
        Assert.Equal(GamePhase.Gathering.ToString(), leaderMessage.CurrentPhase);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KonqvistDbContext>();
        var session = await dbContext.GameSessions.SingleAsync(entity => entity.Id == scenario.GameSessionId);
        Assert.Equal(GameStatus.Running, session.Status);
        Assert.Equal(GamePhase.Gathering, session.CurrentPhase);
        Assert.NotNull(session.StartedAt);
        Assert.NotNull(session.CurrentRoundSessionId);
        Assert.Equal(
            1,
            await dbContext.GameEvents.CountAsync(entity =>
                entity.GameSessionId == scenario.GameSessionId && entity.EventType == "GamePhaseChanged"));
    }

    [Fact]
    public async Task StartGameEndpoint_WhenSessionAlreadyRunning_ReturnsConflict()
    {
        await using var factory = new ServerAppFactory();
        var scenario = await SeedHubScenarioAsync(factory.Services, GamePhase.Gathering, status: GameStatus.Running);

        var gameMasterCookie = await LoginAndGetCookieAsync(factory, scenario.GmToken);
        var client = CreateHttpsClient(factory);
        client.DefaultRequestHeaders.Add("Cookie", gameMasterCookie);

        var response = await client.PostAsync("/api/game/start", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task StartGameEndpoint_WhenSessionWasMarkedRunningButStillWaiting_RepairsStateAndBroadcastsPhaseChanged()
    {
        await using var factory = new ServerAppFactory();
        var scenario = await SeedHubScenarioAsync(factory.Services, GamePhase.WaitingForPlayers, status: GameStatus.Running);

        var runnerCookie = await LoginAndGetCookieAsync(factory, scenario.RunnerToken);
        await using var runnerConnection = CreateHubConnection(factory, runnerCookie);
        var runnerPhaseChanged = new TaskCompletionSource<PhaseChangedMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        runnerConnection.On<PhaseChangedMessage>(nameof(IGameClient.PhaseChanged), message =>
        {
            if (message.GameSessionId == scenario.GameSessionId)
            {
                runnerPhaseChanged.TrySetResult(message);
            }
        });
        await runnerConnection.StartAsync();

        var gameMasterCookie = await LoginAndGetCookieAsync(factory, scenario.GmToken);
        var client = CreateHttpsClient(factory);
        client.DefaultRequestHeaders.Add("Cookie", gameMasterCookie);

        var response = await client.PostAsync("/api/game/start", content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var runnerMessage = await runnerPhaseChanged.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(GamePhase.WaitingForPlayers.ToString(), runnerMessage.PreviousPhase);
        Assert.Equal(GamePhase.Gathering.ToString(), runnerMessage.CurrentPhase);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KonqvistDbContext>();
        var session = await dbContext.GameSessions.SingleAsync(entity => entity.Id == scenario.GameSessionId);
        Assert.Equal(GameStatus.Running, session.Status);
        Assert.Equal(GamePhase.Gathering, session.CurrentPhase);
        Assert.NotNull(session.StartedAt);
        Assert.NotNull(session.CurrentRoundSessionId);
        Assert.Equal(
            1,
            await dbContext.GameEvents.CountAsync(entity =>
                entity.GameSessionId == scenario.GameSessionId && entity.EventType == "GamePhaseChanged"));
    }

    [Fact]
    public async Task Connect_WithCookie_TracksRunnerOnlineStateAcrossConnectAndDisconnect()
    {
        await using var factory = new ServerAppFactory();
        var scenario = await SeedHubScenarioAsync(factory.Services, GamePhase.Gathering);

        var observerCookie = await LoginAndGetCookieAsync(factory, scenario.TeamLeaderToken);
        await using var observerConnection = CreateHubConnection(factory, observerCookie);
        var onlineEvent = new TaskCompletionSource<RunnerStateChangedMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var offlineEvent = new TaskCompletionSource<RunnerStateChangedMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        observerConnection.On<RunnerStateChangedMessage>(nameof(IGameClient.RunnerStateChanged), message =>
        {
            if (message.PlayerSessionId != scenario.RunnerPlayerSessionId)
            {
                return;
            }

            if (message.IsOnline)
            {
                onlineEvent.TrySetResult(message);
            }
            else
            {
                offlineEvent.TrySetResult(message);
            }
        });
        await observerConnection.StartAsync();

        var runnerCookie = await LoginAndGetCookieAsync(factory, scenario.RunnerToken);
        await using var runnerConnection = CreateHubConnection(factory, runnerCookie);
        await runnerConnection.StartAsync();

        var onlineState = await onlineEvent.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(onlineState.IsOnline);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<KonqvistDbContext>();
            var runnerSession = await dbContext.PlayerSessions.SingleAsync(entity => entity.Id == scenario.RunnerPlayerSessionId);
            Assert.True(runnerSession.IsOnline);
        }

        await runnerConnection.StopAsync();

        var offlineState = await offlineEvent.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(offlineState.IsOnline);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<KonqvistDbContext>();
            var runnerSession = await dbContext.PlayerSessions.SingleAsync(entity => entity.Id == scenario.RunnerPlayerSessionId);
            Assert.False(runnerSession.IsOnline);
        }
    }

    [Fact]
    public async Task Connect_WithMultipleRunnerConnections_OnlyMarksOfflineAfterLastDisconnect()
    {
        await using var factory = new ServerAppFactory();
        var scenario = await SeedHubScenarioAsync(factory.Services, GamePhase.Gathering);

        var observerCookie = await LoginAndGetCookieAsync(factory, scenario.TeamLeaderToken);
        await using var observerConnection = CreateHubConnection(factory, observerCookie);
        var onlineEvent = new TaskCompletionSource<RunnerStateChangedMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var offlineEvent = new TaskCompletionSource<RunnerStateChangedMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        observerConnection.On<RunnerStateChangedMessage>(nameof(IGameClient.RunnerStateChanged), message =>
        {
            if (message.PlayerSessionId != scenario.RunnerPlayerSessionId)
            {
                return;
            }

            if (message.IsOnline)
            {
                onlineEvent.TrySetResult(message);
            }
            else
            {
                offlineEvent.TrySetResult(message);
            }
        });
        await observerConnection.StartAsync();

        var runnerCookie = await LoginAndGetCookieAsync(factory, scenario.RunnerToken);
        await using var firstRunnerConnection = CreateHubConnection(factory, runnerCookie);
        await using var secondRunnerConnection = CreateHubConnection(factory, runnerCookie);
        await firstRunnerConnection.StartAsync();
        await onlineEvent.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await secondRunnerConnection.StartAsync();
        await Task.Delay(250);

        await firstRunnerConnection.StopAsync();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<KonqvistDbContext>();
            var runnerSession = await dbContext.PlayerSessions.SingleAsync(entity => entity.Id == scenario.RunnerPlayerSessionId);
            Assert.True(runnerSession.IsOnline);
        }

        await secondRunnerConnection.StopAsync();
        await offlineEvent.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<KonqvistDbContext>();
            var runnerSession = await dbContext.PlayerSessions.SingleAsync(entity => entity.Id == scenario.RunnerPlayerSessionId);
            Assert.False(runnerSession.IsOnline);
        }
    }

    [Fact]
    public async Task Connect_WithoutCookie_IsRejected()
    {
        await using var factory = new ServerAppFactory();
        await SeedHubScenarioAsync(factory.Services, GamePhase.Gathering);

        await using var connection = new HubConnectionBuilder()
            .WithUrl("https://localhost/hubs/game", options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
    }

    [Fact]
    public async Task ClaimDistrict_BroadcastsDistrictClaimed_AndPersistsWalEvent()
    {
        await using var factory = new ServerAppFactory();
        var scenario = await SeedHubScenarioAsync(factory.Services, GamePhase.Gathering);

        var observerCookie = await LoginAndGetCookieAsync(factory, scenario.OtherTeamLeaderToken);
        await using var observerConnection = CreateHubConnection(factory, observerCookie);
        var claimedEvent = new TaskCompletionSource<DistrictClaimedMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        observerConnection.On<DistrictClaimedMessage>(nameof(IGameClient.DistrictClaimed), message =>
        {
            if (message.DistrictSessionId == scenario.DistrictSessionId)
            {
                claimedEvent.TrySetResult(message);
            }
        });
        await observerConnection.StartAsync();

        var runnerCookie = await LoginAndGetCookieAsync(factory, scenario.RunnerToken);
        await using var runnerConnection = CreateHubConnection(factory, runnerCookie);
        await runnerConnection.StartAsync();

        await runnerConnection.InvokeAsync(nameof(GameHub.ClaimDistrict), scenario.DistrictSessionId);

        var message = await claimedEvent.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(scenario.DistrictSessionId, message.DistrictSessionId);
        Assert.Equal(scenario.TeamSessionId, message.TeamSessionId);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KonqvistDbContext>();
        Assert.Equal(
            1,
            await dbContext.GameEvents.CountAsync(entity =>
                entity.GameSessionId == scenario.GameSessionId && entity.EventType == "DistrictClaimed"));
    }

    [Fact]
    public async Task CastVote_BroadcastsVoteCast_AndPersistsWalEvent()
    {
        await using var factory = new ServerAppFactory();
        var scenario = await SeedHubScenarioAsync(factory.Services, GamePhase.Voting, votingEnabled: true);

        var observerCookie = await LoginAndGetCookieAsync(factory, scenario.OtherTeamLeaderToken);
        await using var observerConnection = CreateHubConnection(factory, observerCookie);
        var voteEvent = new TaskCompletionSource<VoteCastMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        observerConnection.On<VoteCastMessage>(nameof(IGameClient.VoteCast), message =>
        {
            if (message.VotingTeamSessionId == scenario.TeamSessionId)
            {
                voteEvent.TrySetResult(message);
            }
        });
        await observerConnection.StartAsync();

        var leaderCookie = await LoginAndGetCookieAsync(factory, scenario.TeamLeaderToken);
        await using var leaderConnection = CreateHubConnection(factory, leaderCookie);
        await leaderConnection.StartAsync();

        await leaderConnection.InvokeAsync(nameof(GameHub.CastVote), scenario.OtherTeamSessionId);

        var message = await voteEvent.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(scenario.TeamSessionId, message.VotingTeamSessionId);
        Assert.Equal(scenario.OtherTeamSessionId, message.TargetTeamSessionId);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KonqvistDbContext>();
        Assert.Equal(
            1,
            await dbContext.GameEvents.CountAsync(entity =>
                entity.GameSessionId == scenario.GameSessionId && entity.EventType == "VoteCast"));
    }

    [Fact]
    public async Task DevelopmentGatheringPhaseTestEndpoint_BroadcastsPhaseChangedToConnectedClients()
    {
        await using var factory = new ServerAppFactory();
        var scenario = await SeedHubScenarioAsync(factory.Services, GamePhase.WaitingForPlayers);

        var runnerCookie = await LoginAndGetCookieAsync(factory, scenario.RunnerToken);
        await using var runnerConnection = CreateHubConnection(factory, runnerCookie);
        var runnerPhaseChanged = new TaskCompletionSource<PhaseChangedMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        runnerConnection.On<PhaseChangedMessage>(nameof(IGameClient.PhaseChanged), message =>
        {
            if (message.GameSessionId == scenario.GameSessionId)
            {
                runnerPhaseChanged.TrySetResult(message);
            }
        });
        await runnerConnection.StartAsync();

        var leaderCookie = await LoginAndGetCookieAsync(factory, scenario.TeamLeaderToken);
        await using var leaderConnection = CreateHubConnection(factory, leaderCookie);
        var leaderPhaseChanged = new TaskCompletionSource<PhaseChangedMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        leaderConnection.On<PhaseChangedMessage>(nameof(IGameClient.PhaseChanged), message =>
        {
            if (message.GameSessionId == scenario.GameSessionId)
            {
                leaderPhaseChanged.TrySetResult(message);
            }
        });
        await leaderConnection.StartAsync();
        await Task.Delay(250);

        var client = CreateHttpsClient(factory);
        client.DefaultRequestHeaders.Add("Cookie", leaderCookie);
        var response = await client.PostAsync($"/api/dev/phase-test/gathering/{scenario.GameSessionId}", content: null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var runnerMessage = await runnerPhaseChanged.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var leaderMessage = await leaderPhaseChanged.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(GamePhase.Gathering.ToString(), runnerMessage.CurrentPhase);
        Assert.Equal(GamePhase.Gathering.ToString(), leaderMessage.CurrentPhase);
    }

    [Fact]
    public async Task DevelopmentResetPhaseTestEndpoint_RewindsSessionAndAllowsStartingAgain()
    {
        await using var factory = new ServerAppFactory();
        var scenario = await SeedHubScenarioAsync(factory.Services, GamePhase.Gathering, status: GameStatus.Running);

        await using (var setupScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = setupScope.ServiceProvider.GetRequiredService<KonqvistDbContext>();
            var round = await dbContext.RoundSessions
                .Include(entity => entity.Votes)
                .OrderBy(entity => entity.Id)
                .FirstAsync(entity => entity.GameSessionId == scenario.GameSessionId);
            var session = await dbContext.GameSessions
                .Include(entity => entity.Teams)
                .Include(entity => entity.Districts)
                .SingleAsync(entity => entity.Id == scenario.GameSessionId);

            round.Status = RoundStatus.Voting;
            round.VotingEnabled = true;
            round.VotingStartedAt = DateTime.UtcNow;
            round.Votes.Add(new Vote
            {
                VotingTeamSessionId = scenario.TeamSessionId,
                TargetTeamSessionId = scenario.OtherTeamSessionId,
                VoteValue = 1,
                IsAutocast = false,
                CastAt = DateTime.UtcNow
            });

            session.Teams.Single(entity => entity.Id == scenario.TeamSessionId).TotalScore = 9;

            var district = session.Districts.Single(entity => entity.Id == scenario.DistrictSessionId);
            district.CurrentOwnerTeamSessionId = scenario.TeamSessionId;
            district.IsClaimedThisRound = true;

            await dbContext.SaveChangesAsync();
        }

        var runnerCookie = await LoginAndGetCookieAsync(factory, scenario.RunnerToken);
        await using var runnerConnection = CreateHubConnection(factory, runnerCookie);
        var runnerPhaseChanged = new TaskCompletionSource<PhaseChangedMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        runnerConnection.On<PhaseChangedMessage>(nameof(IGameClient.PhaseChanged), message =>
        {
            if (message.GameSessionId == scenario.GameSessionId
                && message.CurrentPhase == GamePhase.WaitingForPlayers.ToString())
            {
                runnerPhaseChanged.TrySetResult(message);
            }
        });
        await runnerConnection.StartAsync();

        var gameMasterCookie = await LoginAndGetCookieAsync(factory, scenario.GmToken);
        var client = CreateHttpsClient(factory);
        client.DefaultRequestHeaders.Add("Cookie", gameMasterCookie);

        var resetResponse = await client.PostAsync($"/api/dev/phase-test/reset/{scenario.GameSessionId}", content: null);
        Assert.Equal(HttpStatusCode.NoContent, resetResponse.StatusCode);

        var waitingMessage = await runnerPhaseChanged.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(GamePhase.Gathering.ToString(), waitingMessage.PreviousPhase);
        Assert.Equal(GamePhase.WaitingForPlayers.ToString(), waitingMessage.CurrentPhase);

        await runnerConnection.InvokeAsync(nameof(GameHub.UpdateLocation), 52.1234d, 5.4321d);

        await using (var verifyScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = verifyScope.ServiceProvider.GetRequiredService<KonqvistDbContext>();
            var session = await dbContext.GameSessions
                .Include(entity => entity.Teams)
                .Include(entity => entity.Districts)
                .Include(entity => entity.Rounds)
                .SingleAsync(entity => entity.Id == scenario.GameSessionId);

            Assert.Equal(GameStatus.Pending, session.Status);
            Assert.Equal(GamePhase.WaitingForPlayers, session.CurrentPhase);
            Assert.Null(session.StartedAt);
            Assert.Equal(0, session.Teams.Sum(entity => entity.TotalScore));
            Assert.All(session.Districts, district =>
            {
                Assert.Null(district.CurrentOwnerTeamSessionId);
                Assert.False(district.IsClaimedThisRound);
            });
            Assert.All(session.Rounds, round =>
            {
                Assert.Equal(RoundStatus.Gathering, round.Status);
                Assert.False(round.VotingEnabled);
                Assert.Null(round.VotingStartedAt);
                Assert.Null(round.WinnerTeamSessionId);
            });
            Assert.Equal(0, await dbContext.Votes.CountAsync(entity => entity.RoundSession.GameSessionId == scenario.GameSessionId));
            Assert.Equal(0, await dbContext.GameEvents.CountAsync(entity => entity.GameSessionId == scenario.GameSessionId));

            var runnerSession = await dbContext.PlayerSessions.SingleAsync(entity => entity.Id == scenario.RunnerPlayerSessionId);
            Assert.Null(runnerSession.LocationLat);
            Assert.Null(runnerSession.LocationLng);
            Assert.Null(runnerSession.LocationUpdatedAt);
            Assert.Null(runnerSession.LastSeen);
        }

        var restartResponse = await client.PostAsync("/api/game/start", content: null);
        Assert.Equal(HttpStatusCode.NoContent, restartResponse.StatusCode);
    }

    [Fact]
    public async Task Connect_WithGameMasterCookie_ReceivesLocationAndPhaseUpdates()
    {
        await using var factory = new ServerAppFactory();
        var scenario = await SeedHubScenarioAsync(factory.Services, GamePhase.Gathering);

        var gameMasterCookie = await LoginAndGetCookieAsync(factory, scenario.GmToken);
        await using var gameMasterConnection = CreateHubConnection(factory, gameMasterCookie);
        var locationUpdated = new TaskCompletionSource<LocationUpdatedMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var phaseChanged = new TaskCompletionSource<PhaseChangedMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        gameMasterConnection.On<LocationUpdatedMessage>(nameof(IGameClient.LocationUpdated), message =>
        {
            if (message.PlayerSessionId == scenario.RunnerPlayerSessionId)
            {
                locationUpdated.TrySetResult(message);
            }
        });
        gameMasterConnection.On<PhaseChangedMessage>(nameof(IGameClient.PhaseChanged), message =>
        {
            if (message.GameSessionId == scenario.GameSessionId)
            {
                phaseChanged.TrySetResult(message);
            }
        });
        await gameMasterConnection.StartAsync();

        var runnerCookie = await LoginAndGetCookieAsync(factory, scenario.RunnerToken);
        await using var runnerConnection = CreateHubConnection(factory, runnerCookie);
        await runnerConnection.StartAsync();
        await runnerConnection.InvokeAsync(nameof(GameHub.UpdateLocation), 52.1234d, 5.4321d);

        var locationMessage = await locationUpdated.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(scenario.RunnerPlayerSessionId, locationMessage.PlayerSessionId);
        Assert.Equal(52.1234d, locationMessage.Latitude);
        Assert.Equal(5.4321d, locationMessage.Longitude);

        var client = CreateHttpsClient(factory);
        client.DefaultRequestHeaders.Add("Cookie", gameMasterCookie);
        var response = await client.PostAsync($"/api/dev/phase-test/gathering/{scenario.GameSessionId}", content: null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var phaseMessage = await phaseChanged.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(GamePhase.Gathering.ToString(), phaseMessage.CurrentPhase);
    }

    [Fact]
    public async Task UpdateLocation_UpdatesPlayerSession_AndOnlyBroadcastsImmediatelyToOwnTeam()
    {
        await using var factory = new ServerAppFactory();
        const int minLocationUpdateIntervalSeconds = 5;
        var scenario = await SeedHubScenarioAsync(
            factory.Services,
            GamePhase.Gathering,
            locationBroadcastIntervalSeconds: 300,
            minLocationUpdateIntervalSeconds: minLocationUpdateIntervalSeconds);

        var ownTeamCookie = await LoginAndGetCookieAsync(factory, scenario.TeamLeaderToken);
        await using var ownTeamConnection = CreateHubConnection(factory, ownTeamCookie);
        var ownTeamMessages = new ConcurrentQueue<LocationUpdatedMessage>();
        ownTeamConnection.On<LocationUpdatedMessage>(nameof(IGameClient.LocationUpdated), message =>
        {
            if (message.PlayerSessionId == scenario.RunnerPlayerSessionId)
            {
                ownTeamMessages.Enqueue(message);
            }
        });
        await ownTeamConnection.StartAsync();

        var otherTeamCookie = await LoginAndGetCookieAsync(factory, scenario.OtherTeamLeaderToken);
        await using var otherTeamConnection = CreateHubConnection(factory, otherTeamCookie);
        var otherTeamMessages = new ConcurrentQueue<LocationUpdatedMessage>();
        otherTeamConnection.On<LocationUpdatedMessage>(nameof(IGameClient.LocationUpdated), message =>
        {
            if (message.PlayerSessionId == scenario.RunnerPlayerSessionId)
            {
                otherTeamMessages.Enqueue(message);
            }
        });
        await otherTeamConnection.StartAsync();

        var runnerCookie = await LoginAndGetCookieAsync(factory, scenario.RunnerToken);
        await using var runnerConnection = CreateHubConnection(factory, runnerCookie);
        await runnerConnection.StartAsync();

        await runnerConnection.InvokeAsync(nameof(GameHub.UpdateLocation), 52.1234d, 5.4321d);
        await Task.Delay(250);
        var ownTeamMessageCountAfterFirstUpdate = ownTeamMessages.Count;
        var otherTeamMessageCountAfterFirstUpdate = otherTeamMessages.Count;

        await Task.Delay(TimeSpan.FromSeconds(minLocationUpdateIntervalSeconds + 0.1));
        await runnerConnection.InvokeAsync(nameof(GameHub.UpdateLocation), 52.2234d, 5.5321d);
        await WaitForQueueCountAsync(ownTeamMessages, ownTeamMessageCountAfterFirstUpdate + 1);
        var ownTeamLocation = ownTeamMessages.Last();
        Assert.Equal(52.2234d, ownTeamLocation.Latitude);
        Assert.Equal(5.5321d, ownTeamLocation.Longitude);

        await Task.Delay(500);
        Assert.Equal(otherTeamMessageCountAfterFirstUpdate, otherTeamMessages.Count);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KonqvistDbContext>();
        var runnerSession = await dbContext.PlayerSessions.SingleAsync(entity => entity.Id == scenario.RunnerPlayerSessionId);
        Assert.Equal(52.2234d, runnerSession.LocationLat);
        Assert.Equal(5.5321d, runnerSession.LocationLng);
        Assert.NotNull(runnerSession.LocationUpdatedAt);
    }

    private static HubConnection CreateHubConnection(ServerAppFactory factory, string cookieHeader)
    {
        return new HubConnectionBuilder()
            .WithUrl("https://localhost/hubs/game", options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Headers["Cookie"] = cookieHeader;
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
    }

    private static async Task<string> LoginAndGetCookieAsync(ServerAppFactory factory, string token)
    {
        var client = CreateHttpsClient(factory);
        var response = await client.PostAsJsonAsync("/api/auth/login", new { token });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var cookie = response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.Select(value => value.Split(';', 2)[0]).FirstOrDefault(value => value.StartsWith($"{AuthConstants.CookieName}=", StringComparison.Ordinal))
            : null;
        Assert.False(string.IsNullOrWhiteSpace(cookie));
        return cookie!;
    }

    private static async Task WaitForQueueCountAsync<T>(ConcurrentQueue<T> queue, int expectedCount)
    {
        var timeoutAt = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < timeoutAt)
        {
            if (queue.Count >= expectedCount)
            {
                return;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("Timed out waiting for queued hub messages.");
    }

    private static HttpClient CreateHttpsClient(WebApplicationFactory<ServerEntryPointMarker> factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    private static async Task<HubScenario> SeedHubScenarioAsync(
        IServiceProvider services,
        GamePhase phase,
        GameStatus status = GameStatus.Running,
        bool votingEnabled = false,
        int locationBroadcastIntervalSeconds = 30,
        int minLocationUpdateIntervalSeconds = 5)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KonqvistDbContext>();

        var gameTemplate = new GameTemplate
        {
            Name = $"Hub Template {Guid.NewGuid():N}",
            GmLoginToken = $"GM{Guid.NewGuid():N}"[..10],
            TotalRounds = 1,
            LocationUpdateIntervalSeconds = locationBroadcastIntervalSeconds,
            MinLocationUpdateIntervalSeconds = minLocationUpdateIntervalSeconds,
            VotingDurationSeconds = 60,
            PredictionBonusPoints = 100,
            VoteTimeoutPenalty = 25,
            DistrictCaptureRadiusMeters = 40d
        };

        var runnerToken = $"HR{Guid.NewGuid():N}"[..10];
        var teamLeaderToken = $"HT{Guid.NewGuid():N}"[..10];
        var otherRunnerToken = $"JR{Guid.NewGuid():N}"[..10];
        var otherTeamLeaderToken = $"JT{Guid.NewGuid():N}"[..10];

        var alphaTeam = new TeamTemplate
        {
            Name = $"HubAlpha{Guid.NewGuid():N}"[..12],
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
            Name = $"HubBeta{Guid.NewGuid():N}"[..11],
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
            Name = "Hub District",
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
            Stake = "Hub test stake"
        });

        dbContext.GameTemplates.Add(gameTemplate);
        await dbContext.SaveChangesAsync();

        var session = new GameSession
        {
            GameTemplateId = gameTemplate.Id,
            Status = status,
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

        return new HubScenario(
            session.Id,
            alphaTeamSession.Id,
            betaTeamSession.Id,
            districtSession.Id,
            runnerPlayerSession.Id,
            runnerToken,
            teamLeaderToken,
            otherTeamLeaderToken,
            gameTemplate.GmLoginToken);
    }

    private sealed record HubScenario(
        int GameSessionId,
        int TeamSessionId,
        int OtherTeamSessionId,
        int DistrictSessionId,
        int RunnerPlayerSessionId,
        string RunnerToken,
        string TeamLeaderToken,
        string OtherTeamLeaderToken,
        string GmToken);

    private sealed class ServerAppFactory : WebApplicationFactory<ServerEntryPointMarker>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"konqvist-gamehub-tests-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
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
