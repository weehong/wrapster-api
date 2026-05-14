namespace Wrapsfer.Application.Abstractions.IdentityProvisioning;

public sealed record IdentityLoginResult(
    string AccessToken,
    string UserId,
    string Username,
    bool RequiresPasswordChange);
