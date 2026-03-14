using System.Text.Json;
using Konqvist.Server.Domain.Events;

namespace Konqvist.Server.Domain.Serialization;

internal static class GameEventPayloadSerializer
{
    public static string Serialize(IGameDomainEvent gameEvent)
    {
        return gameEvent switch
        {
            DistrictClaimed districtClaimed => JsonSerializer.Serialize(
                districtClaimed,
                GameAggregateJsonSerializerContext.Default.DistrictClaimed),
            VoteCast voteCast => JsonSerializer.Serialize(
                voteCast,
                GameAggregateJsonSerializerContext.Default.VoteCast),
            VotingOpened votingOpened => JsonSerializer.Serialize(
                votingOpened,
                GameAggregateJsonSerializerContext.Default.VotingOpened),
            VotingClosed votingClosed => JsonSerializer.Serialize(
                votingClosed,
                GameAggregateJsonSerializerContext.Default.VotingClosed),
            RoundAdvanced roundAdvanced => JsonSerializer.Serialize(
                roundAdvanced,
                GameAggregateJsonSerializerContext.Default.RoundAdvanced),
            RunnerLogout runnerLogout => JsonSerializer.Serialize(
                runnerLogout,
                GameAggregateJsonSerializerContext.Default.RunnerLogout),
            RunnerLogin runnerLogin => JsonSerializer.Serialize(
                runnerLogin,
                GameAggregateJsonSerializerContext.Default.RunnerLogin),
            GamePhaseChanged gamePhaseChanged => JsonSerializer.Serialize(
                gamePhaseChanged,
                GameAggregateJsonSerializerContext.Default.GamePhaseChanged),
            _ => throw new InvalidOperationException($"Unsupported game event payload type '{gameEvent.GetType().Name}'.")
        };
    }

    public static IGameDomainEvent Deserialize(string eventType, string payload)
    {
        return eventType switch
        {
            nameof(DistrictClaimed) => JsonSerializer.Deserialize(
                payload,
                GameAggregateJsonSerializerContext.Default.DistrictClaimed)
                ?? throw new InvalidOperationException("Failed to deserialize DistrictClaimed payload."),
            nameof(VoteCast) => JsonSerializer.Deserialize(
                payload,
                GameAggregateJsonSerializerContext.Default.VoteCast)
                ?? throw new InvalidOperationException("Failed to deserialize VoteCast payload."),
            nameof(VotingOpened) => JsonSerializer.Deserialize(
                payload,
                GameAggregateJsonSerializerContext.Default.VotingOpened)
                ?? throw new InvalidOperationException("Failed to deserialize VotingOpened payload."),
            nameof(VotingClosed) => JsonSerializer.Deserialize(
                payload,
                GameAggregateJsonSerializerContext.Default.VotingClosed)
                ?? throw new InvalidOperationException("Failed to deserialize VotingClosed payload."),
            nameof(RoundAdvanced) => JsonSerializer.Deserialize(
                payload,
                GameAggregateJsonSerializerContext.Default.RoundAdvanced)
                ?? throw new InvalidOperationException("Failed to deserialize RoundAdvanced payload."),
            nameof(RunnerLogout) => JsonSerializer.Deserialize(
                payload,
                GameAggregateJsonSerializerContext.Default.RunnerLogout)
                ?? throw new InvalidOperationException("Failed to deserialize RunnerLogout payload."),
            nameof(RunnerLogin) => JsonSerializer.Deserialize(
                payload,
                GameAggregateJsonSerializerContext.Default.RunnerLogin)
                ?? throw new InvalidOperationException("Failed to deserialize RunnerLogin payload."),
            nameof(GamePhaseChanged) => JsonSerializer.Deserialize(
                payload,
                GameAggregateJsonSerializerContext.Default.GamePhaseChanged)
                ?? throw new InvalidOperationException("Failed to deserialize GamePhaseChanged payload."),
            _ => throw new InvalidOperationException($"Unsupported stored event type '{eventType}'.")
        };
    }
}
