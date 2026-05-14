using System.Text.Json.Serialization;

namespace Wrapsfer.Application.Auth.Responses;

public sealed record LoginResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("user")] LoginUserPayload User);
