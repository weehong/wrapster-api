namespace Wrapsfer.Api.Contracts;

public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);
