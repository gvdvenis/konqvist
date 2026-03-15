using System.Text.Json.Serialization;

namespace Konqvist.Server.Features.Game;

[JsonSerializable(typeof(GameErrorResponse))]
public partial class GameJsonSerializerContext : JsonSerializerContext;
