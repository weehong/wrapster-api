using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Billing.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Billing;

/// <summary>
/// Resolves the Stripe customer for the current tenant, creating and persisting the mapping
/// on first use so the same customer is reused across checkouts and portal sessions. Adds the
/// new mapping to the unit of work without saving — the calling handler owns the transaction.
/// </summary>
public sealed class StripeCustomerProvisioner(
    IPartnerBillingCustomerRepository billingCustomerRepository,
    IStripeBillingGateway billingGateway,
    ITenantContext tenantContext)
{
    public async Task<Result<string>> EnsureCustomerIdAsync(string tenantId, CancellationToken cancellationToken)
    {
        PartnerBillingCustomer? existing =
            await billingCustomerRepository.GetByTenantIdAsync(tenantId, cancellationToken);
        if (existing is not null)
        {
            return Result<string>.Success(existing.StripeCustomerId);
        }

        // CreateCustomerAsync is idempotent (keyed on tenantId), so concurrent first-use requests
        // resolve to the same Stripe customer rather than creating duplicates. The PartnerBillingCustomers
        // unique index on TenantId is the backstop for the row: if two requests race the insert, one
        // wins and the loser surfaces a 409 (mapped from the unique-violation), after which a retry
        // finds the existing mapping here and reuses it.
        string stripeCustomerId = await billingGateway.CreateCustomerAsync(
            tenantId, tenantContext.Email, tenantContext.DisplayName, cancellationToken);

        Result<PartnerBillingCustomer> customerResult =
            PartnerBillingCustomer.Create(tenantId, stripeCustomerId);
        if (customerResult.IsFailure)
        {
            return Result<string>.Failure(customerResult.Error);
        }

        billingCustomerRepository.Add(customerResult.Value);
        return Result<string>.Success(stripeCustomerId);
    }
}
