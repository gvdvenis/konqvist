using System.Text.Json.Serialization;

namespace Konqvist.Server.Features.Auth;

[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(AuthIdentityResponse))]
[JsonSerializable(typeof(AuthErrorResponse))]
public partial class AuthJsonSerializerContext : JsonSerializerContext;
