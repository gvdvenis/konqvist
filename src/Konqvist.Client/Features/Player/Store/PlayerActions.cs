using Konqvist.Client.Core.Models;

namespace Konqvist.Client.Features.Player.Store;

public sealed record PlayerIdentityLoadedAction(
    int PlayerSessionId,
    int? TeamSessionId,
    string TeamName,
    PlayerRole Role,
    bool IsLoggedIn,
    bool IsOnline);

public sealed record RunnerStateChangedAction(
    int PlayerSessionId,
    int TeamSessionId,
    bool IsLoggedIn,
    bool IsOnline);

public sealed record RunnerLoggedOutAction(int PlayerSessionId);
