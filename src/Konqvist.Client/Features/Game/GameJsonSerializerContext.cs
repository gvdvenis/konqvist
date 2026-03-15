using System.Text.Json.Serialization;

namespace Konqvist.Client.Features.Game;

[JsonSerializable(typeof(GameErrorResponse))]
public partial class GameJsonSerializerContext : JsonSerializerContext;
