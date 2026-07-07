using Microsoft.EntityFrameworkCore;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Infrastructure.Persistence.Repositories;

internal sealed class FeatureEntitlementRepository(ApplicationDbContext context) : IFeatureEntitlementRepository
{
    public async Task<FeatureEntitlement?> GetActiveAsync(
        string tenantId,
        BillingFeature feature,
        DateTime utcNow,
        CancellationToken cancellationToken = default) =>
        await context.FeatureEntitlements
            .Where(e => e.TenantId == tenantId
                        && e.Feature == feature
                        && e.Status == FeatureEntitlementStatus.Active
                        && e.ValidFromUtc <= utcNow
                        && e.ValidToUtc > utcNow)
            .OrderByDescending(e => e.ValidToUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<FeatureEntitlement?> GetByCheckoutSessionIdAsync(
        string stripeCheckoutSessionId,
        CancellationToken cancellationToken = default) =>
        await context.FeatureEntitlements
            .FirstOrDefaultAsync(e => e.StripeCheckoutSessionId == stripeCheckoutSessionId, cancellationToken);

    public async Task<FeatureEntitlement?> GetByPaymentIntentIdAsync(
        string stripePaymentIntentId,
        CancellationToken cancellationToken = default) =>
        await context.FeatureEntitlements
            .FirstOrDefaultAsync(e => e.StripePaymentIntentId == stripePaymentIntentId, cancellationToken);

    public async Task<IReadOnlyList<FeatureEntitlement>> ListByTenantAndFeatureAsync(
        string tenantId,
        BillingFeature feature,
        CancellationToken cancellationToken = default) =>
        await context.FeatureEntitlements
            .Where(e => e.TenantId == tenantId && e.Feature == feature)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FeatureEntitlement>> ListActiveAsync(
        BillingFeature feature,
        DateTime utcNow,
        CancellationToken cancellationToken = default) =>
        await context.FeatureEntitlements
            .Where(e => e.Feature == feature
                        && e.Status == FeatureEntitlementStatus.Active
                        && e.ValidFromUtc <= utcNow
                        && e.ValidToUtc > utcNow)
            .OrderByDescending(e => e.ValidToUtc)
            .ToListAsync(cancellationToken);

    public void Add(FeatureEntitlement entitlement) => context.FeatureEntitlements.Add(entitlement);
}
