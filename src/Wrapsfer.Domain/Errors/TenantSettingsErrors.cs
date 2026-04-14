using Wrapsfer.Domain.Common;

namespace Wrapsfer.Domain.Errors;

public static class TenantSettingsErrors
{
    public static readonly Error InvalidTenantId = new(
        "TenantSettings.InvalidTenantId",
        "Tenant ID is required",
        ErrorType.Validation);

    public static readonly Error InvalidThreshold = new(
        "TenantSettings.InvalidThreshold",
        "Default low-stock threshold must be greater than or equal to zero",
        ErrorType.Validation);

    public static readonly Error InvalidEmail = new(
        "TenantSettings.InvalidEmail",
        "Recipient email is invalid",
        ErrorType.Validation);

    public static readonly Error RecipientAlreadyExists = new(
        "TenantSettings.RecipientAlreadyExists",
        "A recipient with this email already exists for the tenant",
        ErrorType.Conflict);

    public static readonly Error RecipientNotFound = new(
        "TenantSettings.RecipientNotFound",
        "The recipient was not found",
        ErrorType.NotFound);

    public static readonly Error NotFound = new(
        "TenantSettings.NotFound",
        "Tenant settings were not found",
        ErrorType.NotFound);
}
