namespace Wrapsfer.Application.Abstractions.IdentityProvisioning;

public sealed record IdentityLoginResult(
    string AccessToken,
    int ExpiresIn,
    int? RefreshExpiresIn,
    string? RefreshToken,
    string TokenType,
    int? NotBeforePolicy,
    string? SessionState,
    string? Scope,
    string UserId,
    string Username,
    bool RequiresPasswordChange);
