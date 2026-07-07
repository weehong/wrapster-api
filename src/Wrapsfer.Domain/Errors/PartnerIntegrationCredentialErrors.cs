using Wrapsfer.Domain.Common;

namespace Wrapsfer.Domain.Errors;

public static class PartnerIntegrationCredentialErrors
{
    public static readonly Error PartnerTenantNotFound = new(
        "PartnerIntegrationCredential.PartnerTenantNotFound",
        "The partner tenant was not found",
        ErrorType.NotFound);

    public static readonly Error NotFound = new(
        "PartnerIntegrationCredential.NotFound",
        "The integration credential was not found",
        ErrorType.NotFound);

    public static readonly Error AlreadyExists = new(
        "PartnerIntegrationCredential.AlreadyExists",
        "An integration credential already exists for this tenant",
        ErrorType.Conflict);

    public static readonly Error Disabled = new(
        "PartnerIntegrationCredential.Disabled",
        "The integration credential is disabled",
        ErrorType.Conflict);

    public static readonly Error AlreadyDisabled = new(
        "PartnerIntegrationCredential.AlreadyDisabled",
        "The integration credential is already disabled",
        ErrorType.Conflict);

    public static readonly Error AlreadyEnabled = new(
        "PartnerIntegrationCredential.AlreadyEnabled",
        "The integration credential is already enabled",
        ErrorType.Conflict);

    public static readonly Error IdentityProviderFailure = new(
        "PartnerIntegrationCredential.IdentityProviderFailure",
        "The identity provider rejected the integration credential operation",
        ErrorType.Failure);

    public static readonly Error CreationFailed = new(
        "PartnerIntegrationCredential.CreationFailed",
        "Failed to create the integration credential. Please retry",
        ErrorType.Failure);

    public static readonly Error OwnerRealmNotAllowed = new(
        "PartnerIntegrationCredential.OwnerRealmNotAllowed",
        "The owner realm cannot have partner integration credentials",
        ErrorType.Validation);

    public static readonly Error InvalidTenantId = new(
        "PartnerIntegrationCredential.InvalidTenantId",
        "Tenant ID is required",
        ErrorType.Validation);

    public static readonly Error InvalidClientId = new(
        "PartnerIntegrationCredential.InvalidClientId",
        "Client ID is required",
        ErrorType.Validation);

    public static readonly Error InvalidKeycloakClientUuid = new(
        "PartnerIntegrationCredential.InvalidKeycloakClientUuid",
        "Keycloak client UUID is required",
        ErrorType.Validation);

    public static readonly Error InvalidDisplayName = new(
        "PartnerIntegrationCredential.InvalidDisplayName",
        "Display name is required and must be at most 256 characters",
        ErrorType.Validation);

    public static readonly Error SecretNotRetrievable = new(
        "PartnerIntegrationCredential.SecretNotRetrievable",
        "An existing client secret cannot be revealed. Rotate the credential to obtain a new secret",
        ErrorType.Conflict);
}
