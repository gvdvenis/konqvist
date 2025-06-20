﻿namespace Konqvist.Web.SignalR;

public interface IBindableHubClient : IGameHubClient, IGameHubServer
{
    Func<DistrictOwner, Task>? OnDistrictOwnerChanged { get; set; }

    Func<ActorLocation, Task>? OnActorMoved { get; set; }

    Func<Task>? OnRunnerLoggedInOrOut { get; set; }

    Func<string, Task>? OnRunnerLoggedIn { get; set; }

    Func<string[], Task>? OnRunnersLoggedOut { get; set; }

    Func<RoundData, Task>? OnNewRoundStarted { get; set; }

    Func<string?, Task>? OnTeamResourcesChanged { get; set; }

    Func<List<TeamVote>, string?, Task>? OnVotesUpdated { get; set; }

    Func<Task>? OnVotingStarted { get; set; }
}