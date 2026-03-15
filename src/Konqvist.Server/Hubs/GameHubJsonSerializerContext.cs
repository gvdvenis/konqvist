using System.Text.Json.Serialization;

namespace Konqvist.Server.Hubs;

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
