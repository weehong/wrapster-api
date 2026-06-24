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
