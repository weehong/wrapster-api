namespace Wrapsfer.Api.Contracts;

public sealed record LoginRequest(
    string Username,
    string Password);
