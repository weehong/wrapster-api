using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Domain.Entities;

public sealed class PartnerIntegrationCredential : AuditableEntity
{
    private const int DisplayNameMaxLength = 256;

    private PartnerIntegrationCredential()
    {
    }

    public string TenantId { get; private set; } = default!;
    public string ClientId { get; private set; } = default!;
    public string KeycloakClientUuid { get; private set; } = default!;
    public string DisplayName { get; private set; } = default!;
    public bool IsEnabled { get; private set; }
    public DateTime? LastRotatedAt { get; private set; }
    public string? LastRotatedBy { get; private set; }
    public DateTime? DisabledAt { get; private set; }
    public string? DisabledBy { get; private set; }

    public static Result<PartnerIntegrationCredential> Create(
        string tenantId,
        string clientId,
        string keycloakClientUuid,
        string displayName)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result<PartnerIntegrationCredential>.Failure(PartnerIntegrationCredentialErrors.InvalidTenantId);
        }

        if (string.IsNullOrWhiteSpace(clientId))
        {
            return Result<PartnerIntegrationCredential>.Failure(PartnerIntegrationCredentialErrors.InvalidClientId);
        }

        if (string.IsNullOrWhiteSpace(keycloakClientUuid))
        {
            return Result<PartnerIntegrationCredential>.Failure(
                PartnerIntegrationCredentialErrors.InvalidKeycloakClientUuid);
        }

        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > DisplayNameMaxLength)
        {
            return Result<PartnerIntegrationCredential>.Failure(
                PartnerIntegrationCredentialErrors.InvalidDisplayName);
        }

        PartnerIntegrationCredential credential = new()
        {
            TenantId = tenantId,
            ClientId = clientId,
            KeycloakClientUuid = keycloakClientUuid,
            DisplayName = displayName.Trim(),
            IsEnabled = true
        };

        return Result<PartnerIntegrationCredential>.Success(credential);
    }

    public Result MarkRotated(DateTime rotatedAt, string? rotatedBy)
    {
        if (!IsEnabled)
        {
            return Result.Failure(PartnerIntegrationCredentialErrors.Disabled);
        }

        LastRotatedAt = rotatedAt;
        LastRotatedBy = rotatedBy;
        return Result.Success();
    }

    public Result Disable(DateTime disabledAt, string? disabledBy)
    {
        if (!IsEnabled)
        {
            return Result.Failure(PartnerIntegrationCredentialErrors.AlreadyDisabled);
        }

        IsEnabled = false;
        DisabledAt = disabledAt;
        DisabledBy = disabledBy;
        return Result.Success();
    }

    public Result Enable()
    {
        if (IsEnabled)
        {
            return Result.Failure(PartnerIntegrationCredentialErrors.AlreadyEnabled);
        }

        IsEnabled = true;
        DisabledAt = null;
        DisabledBy = null;
        return Result.Success();
    }
}
