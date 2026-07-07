namespace Wrapsfer.Infrastructure.Authentication;

public static class AuthorizationPolicies
{
    public const string OwnerAdminOnly = "OwnerAdminOnly";
    public const string IntegrationApiOnly = "IntegrationApiOnly";
    public const string PartnerIntegrationAdmin = "PartnerIntegrationAdmin";
}
