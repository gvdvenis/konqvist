using System.Text.Json.Serialization;

namespace Konqvist.Server.Features.SessionState;

[JsonSerializable(typeof(SessionStateResponse))]
public partial class SessionStateJsonSerializerContext : JsonSerializerContext;
