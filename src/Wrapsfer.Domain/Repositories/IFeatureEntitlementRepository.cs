using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Domain.Repositories;

public interface IFeatureEntitlementRepository
{
    /// <summary>
    /// Returns the active entitlement for the tenant and feature that covers <paramref name="utcNow"/>,
    /// or null when the tenant has no active access.
    /// </summary>
    Task<FeatureEntitlement?> GetActiveAsync(
        string tenantId,
        BillingFeature feature,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<FeatureEntitlement?> GetByCheckoutSessionIdAsync(
        string stripeCheckoutSessionId,
        CancellationToken cancellationToken = default);

    Task<FeatureEntitlement?> GetByPaymentIntentIdAsync(
        string stripePaymentIntentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FeatureEntitlement>> ListByTenantAndFeatureAsync(
        string tenantId,
        BillingFeature feature,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every tenant's active entitlement for the feature that covers <paramref name="utcNow"/>.
    /// Used by owner-facing aggregate views to avoid per-tenant round trips.
    /// </summary>
    Task<IReadOnlyList<FeatureEntitlement>> ListActiveAsync(
        BillingFeature feature,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    void Add(FeatureEntitlement entitlement);
}
