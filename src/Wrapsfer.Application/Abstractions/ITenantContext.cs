namespace Wrapsfer.Application.Abstractions;

public interface ITenantContext
{
    string TenantId { get; }
    string UserId { get; }
    string Username { get; }
    string? DisplayName { get; }
    string? Email { get; }
    IReadOnlyList<string> Roles { get; }
}
