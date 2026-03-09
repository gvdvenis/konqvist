namespace Konqvist.Infrastructure.Entities.Session;

public class GameEvent
{
    public int Id { get; set; }
    public int GameSessionId { get; set; }
    public int? RoundSessionId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public int? ActorPlayerSessionId { get; set; }

    public GameSession GameSession { get; set; } = null!;
    public RoundSession? RoundSession { get; set; }
    public PlayerSession? ActorPlayerSession { get; set; }
}
