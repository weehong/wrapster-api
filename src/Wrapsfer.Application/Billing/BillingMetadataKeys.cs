namespace Wrapsfer.Application.Billing;

/// <summary>Stripe metadata keys attached to checkout sessions for traceability and reconciliation.</summary>
public static class BillingMetadataKeys
{
    public const string TenantId = "tenantId";
    public const string Feature = "feature";
}
