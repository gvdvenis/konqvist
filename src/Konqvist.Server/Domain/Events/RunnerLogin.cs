namespace Konqvist.Server.Domain.Events;

public sealed record RunnerLogin(
    int GameSessionId,
    int PlayerSessionId,
    DateTime OccurredAt) : IGameDomainEvent
{
    public string EventType => nameof(RunnerLogin);

    public int? RoundSessionId => null;

    public int? ActorPlayerSessionId => PlayerSessionId;
}
