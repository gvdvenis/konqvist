namespace Konqvist.Server.Domain.Events;

public sealed record RunnerLogout(
    int GameSessionId,
    int TargetPlayerSessionId,
    int? ActorPlayerSessionId,
    DateTime OccurredAt) : IGameDomainEvent
{
    public string EventType => nameof(RunnerLogout);

    public int? RoundSessionId => null;
}
