using System.Text.Json.Serialization;
using Konqvist.Server.Domain.Events;

namespace Konqvist.Server.Domain.Serialization;

[JsonSerializable(typeof(DistrictClaimed))]
[JsonSerializable(typeof(VoteCast))]
[JsonSerializable(typeof(VotingOpened))]
[JsonSerializable(typeof(VotingClosed))]
[JsonSerializable(typeof(RoundAdvanced))]
[JsonSerializable(typeof(RunnerLogout))]
[JsonSerializable(typeof(RunnerLogin))]
[JsonSerializable(typeof(GamePhaseChanged))]
public partial class GameAggregateJsonSerializerContext : JsonSerializerContext;
