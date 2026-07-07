using System.Text.Json.Serialization;

namespace Wrapsfer.Application.Auth.Responses;

public sealed record LoginUserPayload(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("requires_password_change")]
    bool RequiresPasswordChange);
