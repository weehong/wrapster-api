using Wrapster.Domain.Common;
using Wrapster.Domain.Errors;

namespace Wrapster.Domain.Entities;

public sealed class NotificationRecipient : AuditableEntity
{
    private NotificationRecipient()
    {
    }

    public string TenantId { get; private set; } = default!;
    public Guid TenantSettingsId { get; private set; }
    public string Email { get; private set; } = default!;
    public bool IsActive { get; private set; }

    public static Result<NotificationRecipient> Create(string tenantId, Guid tenantSettingsId, string email)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result<NotificationRecipient>.Failure(TenantSettingsErrors.InvalidTenantId);
        }

        if (!IsValidEmail(email))
        {
            return Result<NotificationRecipient>.Failure(TenantSettingsErrors.InvalidEmail);
        }

        return Result<NotificationRecipient>.Success(new NotificationRecipient
        {
            TenantId = tenantId,
            TenantSettingsId = tenantSettingsId,
            Email = email.Trim(),
            IsActive = true
        });
    }

    public void SetActive(bool isActive) => IsActive = isActive;

    private static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        int atIndex = email.IndexOf('@');
        return atIndex > 0 && atIndex < email.Length - 1 && !email.Contains(' ');
    }
}
