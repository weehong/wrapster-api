using Wrapsfer.Domain.Common;

namespace Wrapsfer.Domain.Errors;

public static class PartnerTenantErrors
{
    public static readonly Error InvalidTenantId = new(
        "PartnerTenant.InvalidTenantId",
        "Tenant ID must be 3-63 lowercase alphanumeric characters or dashes, and cannot start or end with a dash",
        ErrorType.Validation);

    public static readonly Error InvalidDisplayName = new(
        "PartnerTenant.InvalidDisplayName",
        "Display name is required",
        ErrorType.Validation);

    public static readonly Error InvalidContactEmail = new(
        "PartnerTenant.InvalidContactEmail",
        "Contact email is invalid",
        ErrorType.Validation);

    public static readonly Error AlreadyExists = new(
        "PartnerTenant.AlreadyExists",
        "A partner tenant with this tenant ID already exists",
        ErrorType.Conflict);

    public static readonly Error NotFound = new(
        "PartnerTenant.NotFound",
        "The partner tenant was not found",
        ErrorType.NotFound);

    public static readonly Error OwnerRealmNotAllowed = new(
        "PartnerTenant.OwnerRealmNotAllowed",
        "The owner realm cannot be used as a partner tenant",
        ErrorType.Validation);

    public static readonly Error AlreadyActive = new(
        "PartnerTenant.AlreadyActive",
        "The partner tenant is already active",
        ErrorType.Conflict);

    public static readonly Error AlreadyInactive = new(
        "PartnerTenant.AlreadyInactive",
        "The partner tenant is already inactive",
        ErrorType.Conflict);

    public static readonly Error ProvisioningFailed = new(
        "PartnerTenant.ProvisioningFailed",
        "Failed to provision the partner tenant. The provisioning record has been marked as failed for retry",
        ErrorType.Failure);

    public static readonly Error NotRetryable = new(
        "PartnerTenant.NotRetryable",
        "Only partner tenants in a Failed provisioning state can be retried",
        ErrorType.Conflict);

    public static readonly Error CannotModifyInactive = new(
        "PartnerTenant.CannotModifyInactive",
        "A deactivated partner tenant cannot transition to this state",
        ErrorType.Conflict);
}
