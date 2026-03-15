using System.Text.Json.Serialization;
using Konqvist.Client.Core.State;

namespace Konqvist.Client.Core.SignalR;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ClientStateSnapshot))]
[JsonSerializable(typeof(GameStartedMessage))]
[JsonSerializable(typeof(DistrictClaimedMessage))]
[JsonSerializable(typeof(DistrictOwnershipChangedMessage))]
[JsonSerializable(typeof(PhaseChangedMessage))]
[JsonSerializable(typeof(VoteStartedMessage))]
[JsonSerializable(typeof(VoteCastMessage))]
[JsonSerializable(typeof(VoteEndedMessage))]
[JsonSerializable(typeof(ScoreUpdatedMessage))]
[JsonSerializable(typeof(GameStateChangedMessage))]
[JsonSerializable(typeof(RoundEndedMessage))]
[JsonSerializable(typeof(RunnerLoggedOutMessage))]
[JsonSerializable(typeof(LocationUpdatedMessage))]
[JsonSerializable(typeof(RunnerStateChangedMessage))]
public partial class GameHubJsonSerializerContext : JsonSerializerContext;
