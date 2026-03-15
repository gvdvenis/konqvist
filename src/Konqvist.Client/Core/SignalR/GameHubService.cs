using System.Net.Http.Json;
using Fluxor;
using Konqvist.Client.Core.Models;
using Konqvist.Client.Core.State;
using Konqvist.Client.Features.Game.Store;
using Konqvist.Client.Features.Map.Store;
using Konqvist.Client.Features.Player.Store;
using Konqvist.Client.Features.Scores.Store;
using Konqvist.Client.Features.Voting.Store;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Microsoft.AspNetCore.SignalR.Client;

namespace Konqvist.Client.Core.SignalR;

public sealed class GameHubService(
    HttpClient httpClient,
    IDispatcher dispatcher,
    ILogger<GameHubService> logger) : IAsyncDisposable
{
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private readonly ConnectionStateObservable _connectionState = new(HubConnectionState.Disconnected);
    private HubConnection? _connection;

    public IObservable<HubConnectionState> ConnectionState => _connectionState;

    public HubConnectionState CurrentConnectionState => _connectionState.CurrentValue;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            _connection ??= CreateConnection();
            if (_connection.State is HubConnectionState.Connected or HubConnectionState.Connecting or HubConnectionState.Reconnecting)
            {
                return;
            }

            try
            {
                await _connection.StartAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Game hub connection start failed.");

                if (_connection.State is HubConnectionState.Connected or HubConnectionState.Connecting or HubConnectionState.Reconnecting)
                {
                    await _connection.StopAsync(cancellationToken);
                }

                PublishConnectionState(HubConnectionState.Disconnected);
                return;
            }

            await SyncConnectionStateAsync(_connection, "initial connect", cancellationToken);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (_connection is null)
            {
                PublishConnectionState(HubConnectionState.Disconnected);
                return;
            }

            if (_connection.State is HubConnectionState.Connected or HubConnectionState.Connecting or HubConnectionState.Reconnecting)
            {
                await _connection.StopAsync(cancellationToken);
            }

            PublishConnectionState(HubConnectionState.Disconnected);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }

        _connectionState.Dispose();
        _lifecycleLock.Dispose();
    }

    private HubConnection CreateConnection()
    {
        var hubUri = new Uri(httpClient.BaseAddress ?? throw new InvalidOperationException("HttpClient BaseAddress is not configured."), "/hubs/game");

        var connection = new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.HttpMessageHandlerFactory = innerHandler =>
                    new IncludeCredentialsHttpMessageHandler { InnerHandler = innerHandler };
            })
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.TypeInfoResolverChain.Insert(0, GameHubJsonSerializerContext.Default);
                options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
            })
            .WithAutomaticReconnect()
            .Build();

        RegisterEventHandlers(connection);
        connection.Reconnecting += error =>
        {
            logger.LogWarning(error, "Game hub connection is reconnecting.");
            PublishConnectionState(HubConnectionState.Reconnecting);
            return Task.CompletedTask;
        };
        connection.Reconnected += async _ =>
        {
            logger.LogInformation("Game hub connection reconnected.");
            await SyncConnectionStateAsync(connection, "reconnect", disconnectOnFailure: false);
        };
        connection.Closed += error =>
        {
            if (error is not null)
            {
                logger.LogWarning(error, "Game hub connection closed.");
            }

            PublishConnectionState(HubConnectionState.Disconnected);
            return Task.CompletedTask;
        };

        return connection;
    }

    private void RegisterEventHandlers(HubConnection connection)
    {
        connection.On<GameStartedMessage>(GameHubMethodNames.GameStarted, message =>
        {
            dispatcher.Dispatch(new GameStartedAction(
                message.GameSessionId,
                message.RoundNumber,
                ParseGamePhase(message.Phase)));
        });

        connection.On<DistrictClaimedMessage>(GameHubMethodNames.DistrictClaimed, message =>
        {
            dispatcher.Dispatch(new DistrictClaimedAction(message.DistrictSessionId, message.TeamSessionId));
        });

        connection.On<DistrictOwnershipChangedMessage>(GameHubMethodNames.DistrictOwnershipChanged, message =>
        {
            dispatcher.Dispatch(new DistrictOwnershipChangedAction(message.DistrictSessionId, message.CurrentTeamSessionId));
        });

        connection.On<PhaseChangedMessage>(GameHubMethodNames.PhaseChanged, message =>
        {
            var currentPhase = ParseGamePhase(message.CurrentPhase);

            dispatcher.Dispatch(new GamePhaseChangedAction(
                message.GameSessionId,
                currentPhase,
                null));
            dispatcher.Dispatch(new NavigateToPhaseAction(currentPhase));
        });

        connection.On<VoteStartedMessage>(GameHubMethodNames.VoteStarted, _ =>
        {
            dispatcher.Dispatch(new VotingOpenedAction(null));
        });

        connection.On<VoteCastMessage>(GameHubMethodNames.VoteCast, message =>
        {
            dispatcher.Dispatch(new VoteCastAction(
                message.VotingTeamSessionId,
                message.TargetTeamSessionId,
                message.VoteValue));
        });

        connection.On<VoteEndedMessage>(GameHubMethodNames.VoteEnded, _ =>
        {
            dispatcher.Dispatch(new VotingClosedAction());
        });

        connection.On<ScoreUpdatedMessage>(GameHubMethodNames.ScoreUpdated, message =>
        {
            dispatcher.Dispatch(new ScoreUpdatedAction(
                message.TeamSessionId,
                message.TotalScore,
                new TeamResourceTotals(
                    message.TotalGold,
                    message.TotalVoters,
                    message.TotalLikes,
                    message.TotalOil)));
        });

        connection.On<GameStateChangedMessage>(GameHubMethodNames.GameStateChanged, message =>
        {
            dispatcher.Dispatch(new GameStateChangedAction(
                message.GameSessionId,
                message.CurrentRoundNumber,
                ParseGamePhase(message.CurrentPhase)));
        });

        connection.On<RoundEndedMessage>(GameHubMethodNames.RoundEnded, message =>
        {
            dispatcher.Dispatch(new RoundEndedAction(
                message.GameSessionId,
                message.RoundNumber,
                ParseGamePhase(message.CurrentPhase)));
        });

        connection.On<RunnerLoggedOutMessage>(GameHubMethodNames.RunnerLoggedOut, message =>
        {
            dispatcher.Dispatch(new RunnerLoggedOutAction(message.TargetPlayerSessionId));
        });

        connection.On<LocationUpdatedMessage>(GameHubMethodNames.LocationUpdated, message =>
        {
            dispatcher.Dispatch(new LocationUpdatedAction(
                message.PlayerSessionId,
                message.TeamSessionId,
                message.Latitude,
                message.Longitude));
        });

        connection.On<RunnerStateChangedMessage>(GameHubMethodNames.RunnerStateChanged, message =>
        {
            dispatcher.Dispatch(new RunnerStateChangedAction(
                message.PlayerSessionId,
                message.TeamSessionId,
                message.IsLoggedIn,
                message.IsOnline));
        });
    }

    private async Task SyncFullStateAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/session/state");
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var snapshot = await response.Content.ReadFromJsonAsync(
            GameHubJsonSerializerContext.Default.ClientStateSnapshot,
            cancellationToken)
            ?? throw new InvalidOperationException("Session state response was empty.");

        dispatcher.Dispatch(new FullStateSyncAction(snapshot));
    }

    private async Task<bool> SyncConnectionStateAsync(
        HubConnection connection,
        string syncReason,
        CancellationToken cancellationToken = default,
        bool disconnectOnFailure = true)
    {
        try
        {
            await SyncFullStateAsync(cancellationToken);
            PublishConnectionState(HubConnectionState.Connected);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Game hub state sync failed during {SyncReason}.", syncReason);

            if (disconnectOnFailure)
            {
                if (connection.State is HubConnectionState.Connected or HubConnectionState.Connecting or HubConnectionState.Reconnecting)
                {
                    await connection.StopAsync(cancellationToken);
                }

                PublishConnectionState(HubConnectionState.Disconnected);
                return false;
            }

            PublishConnectionState(connection.State);
            return false;
        }
    }

    private void PublishConnectionState(HubConnectionState state) => _connectionState.Publish(state);

    private static GamePhase ParseGamePhase(string phase)
    {
        if (Enum.TryParse<GamePhase>(phase, ignoreCase: true, out var gamePhase))
        {
            return gamePhase;
        }

        throw new InvalidOperationException($"Received unsupported game phase '{phase}'.");
    }

    private sealed class IncludeCredentialsHttpMessageHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
            return base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class ConnectionStateObservable(HubConnectionState initialValue) : IObservable<HubConnectionState>, IDisposable
    {
        private readonly object _syncRoot = new();
        private readonly List<IObserver<HubConnectionState>> _observers = [];
        private bool _disposed;

        public HubConnectionState CurrentValue { get; private set; } = initialValue;

        public IDisposable Subscribe(IObserver<HubConnectionState> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);

            lock (_syncRoot)
            {
                ThrowIfDisposed();
                _observers.Add(observer);
            }

            observer.OnNext(CurrentValue);
            return new Subscription(this, observer);
        }

        public void Publish(HubConnectionState state)
        {
            IObserver<HubConnectionState>[] observers;
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                CurrentValue = state;
                observers = [.. _observers];
            }

            foreach (var observer in observers)
            {
                observer.OnNext(state);
            }
        }

        public void Dispose()
        {
            IObserver<HubConnectionState>[] observers;
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                observers = [.. _observers];
                _observers.Clear();
            }

            foreach (var observer in observers)
            {
                observer.OnCompleted();
            }
        }

        private void Unsubscribe(IObserver<HubConnectionState> observer)
        {
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                _observers.Remove(observer);
            }
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, typeof(ConnectionStateObservable));
        }

        private sealed class Subscription(ConnectionStateObservable owner, IObserver<HubConnectionState> observer) : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                owner.Unsubscribe(observer);
            }
        }
    }
}
