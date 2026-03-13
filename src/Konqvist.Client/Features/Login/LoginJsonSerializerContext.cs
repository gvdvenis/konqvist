using System.Text.Json.Serialization;

namespace Konqvist.Client.Features.Login;

[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(AuthErrorResponse))]
[JsonSerializable(typeof(AuthIdentityResponse))]
[JsonSerializable(typeof(TeamStatusResponse))]
public partial class LoginJsonSerializerContext : JsonSerializerContext;
