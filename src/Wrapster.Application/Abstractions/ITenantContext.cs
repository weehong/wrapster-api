namespace Wrapster.Application.Abstractions;

public interface ITenantContext
{
    string TenantId { get; }
    string UserId { get; }
    IReadOnlyList<string> Roles { get; }
}
