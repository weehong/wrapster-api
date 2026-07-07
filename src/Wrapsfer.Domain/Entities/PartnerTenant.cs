using System.Net.Mail;
using System.Text.RegularExpressions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Domain.Entities;

public sealed partial class PartnerTenant : AuditableEntity
{
    private const int TenantIdMinLength = 3;
    private const int TenantIdMaxLength = 63;
    private const int DisplayNameMaxLength = 256;
    private const int ContactEmailMaxLength = 320;

    private PartnerTenant()
    {
    }

    public string TenantId { get; private set; } = default!;
    public string DisplayName { get; private set; } = default!;
    public string? ContactEmail { get; private set; }
    public bool IsActive { get; private set; }
    public PartnerTenantProvisioningStatus ProvisioningStatus { get; private set; }
    public DateTime? DeactivatedAt { get; private set; }
    public string? LastProvisioningError { get; private set; }

    public static Result<PartnerTenant> Create(
        string tenantId,
        string displayName,
        string ownerRealm,
        string? contactEmail = null)
    {
        if (!IsValidTenantId(tenantId))
        {
            return Result<PartnerTenant>.Failure(PartnerTenantErrors.InvalidTenantId);
        }

        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > DisplayNameMaxLength)
        {
            return Result<PartnerTenant>.Failure(PartnerTenantErrors.InvalidDisplayName);
        }

        if (string.Equals(tenantId, ownerRealm, StringComparison.OrdinalIgnoreCase))
        {
            return Result<PartnerTenant>.Failure(PartnerTenantErrors.OwnerRealmNotAllowed);
        }

        if (contactEmail is not null && !IsValidEmail(contactEmail))
        {
            return Result<PartnerTenant>.Failure(PartnerTenantErrors.InvalidContactEmail);
        }

        PartnerTenant partner = new()
        {
            TenantId = tenantId,
            DisplayName = displayName.Trim(),
            ContactEmail = contactEmail?.Trim(),
            IsActive = false,
            ProvisioningStatus = PartnerTenantProvisioningStatus.Provisioning
        };

        return Result<PartnerTenant>.Success(partner);
    }

    public Result MarkActive()
    {
        if (ProvisioningStatus == PartnerTenantProvisioningStatus.Active && IsActive)
        {
            return Result.Failure(PartnerTenantErrors.AlreadyActive);
        }

        IsActive = true;
        ProvisioningStatus = PartnerTenantProvisioningStatus.Active;
        DeactivatedAt = null;
        LastProvisioningError = null;
        return Result.Success();
    }

    public Result MarkProvisioningFailed(string? errorDetail)
    {
        ProvisioningStatus = PartnerTenantProvisioningStatus.Failed;
        IsActive = false;
        LastProvisioningError = string.IsNullOrWhiteSpace(errorDetail)
            ? null
            : errorDetail.Length > 1024
                ? errorDetail[..1024]
                : errorDetail;
        return Result.Success();
    }

    public Result BeginRetry()
    {
        if (ProvisioningStatus != PartnerTenantProvisioningStatus.Failed)
        {
            return Result.Failure(PartnerTenantErrors.NotRetryable);
        }

        ProvisioningStatus = PartnerTenantProvisioningStatus.Provisioning;
        LastProvisioningError = null;
        return Result.Success();
    }

    public Result UpdateProfile(string displayName, string? contactEmail)
    {
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > DisplayNameMaxLength)
        {
            return Result.Failure(PartnerTenantErrors.InvalidDisplayName);
        }

        if (contactEmail is not null && !IsValidEmail(contactEmail))
        {
            return Result.Failure(PartnerTenantErrors.InvalidContactEmail);
        }

        DisplayName = displayName.Trim();
        ContactEmail = string.IsNullOrWhiteSpace(contactEmail) ? null : contactEmail.Trim();
        return Result.Success();
    }

    public Result Deactivate(DateTime deactivatedAt)
    {
        if (!IsActive || ProvisioningStatus == PartnerTenantProvisioningStatus.Deactivated)
        {
            return Result.Failure(PartnerTenantErrors.AlreadyInactive);
        }

        IsActive = false;
        ProvisioningStatus = PartnerTenantProvisioningStatus.Deactivated;
        DeactivatedAt = deactivatedAt;
        return Result.Success();
    }

    public Result Reactivate()
    {
        if (ProvisioningStatus == PartnerTenantProvisioningStatus.Failed)
        {
            return Result.Failure(PartnerTenantErrors.CannotModifyInactive);
        }

        if (IsActive && ProvisioningStatus == PartnerTenantProvisioningStatus.Active)
        {
            return Result.Failure(PartnerTenantErrors.AlreadyActive);
        }

        IsActive = true;
        ProvisioningStatus = PartnerTenantProvisioningStatus.Active;
        DeactivatedAt = null;
        return Result.Success();
    }

    private static bool IsValidTenantId(string? tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return false;
        }

        if (tenantId.Length < TenantIdMinLength || tenantId.Length > TenantIdMaxLength)
        {
            return false;
        }

        return TenantIdRegex().IsMatch(tenantId);
    }

    private static bool IsValidEmail(string email)
    {
        string trimmed = email.Trim();

        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > ContactEmailMaxLength)
        {
            return false;
        }

        try
        {
            MailAddress mailAddress = new(trimmed);
            return string.Equals(mailAddress.Address, trimmed, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex TenantIdRegex();
}
