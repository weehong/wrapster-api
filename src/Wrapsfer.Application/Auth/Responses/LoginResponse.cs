using System.Text.Json.Serialization;

namespace Wrapsfer.Application.Auth.Responses;

public sealed record LoginResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("refresh_expires_in")] int? RefreshExpiresIn,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("not-before-policy")] int? NotBeforePolicy,
    [property: JsonPropertyName("session_state")] string? SessionState,
    [property: JsonPropertyName("scope")] string? Scope,
    [property: JsonPropertyName("user")] LoginUserPayload User);
