using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Domain.Entities;

/// <summary>
/// Maps a partner tenant to its Stripe customer so the same customer is reused across
/// checkouts and billing-portal sessions rather than recreated each time.
/// </summary>
public sealed class PartnerBillingCustomer : AuditableEntity
{
    private const int TenantIdMaxLength = 256;
    private const int StripeCustomerIdMaxLength = 256;

    private PartnerBillingCustomer()
    {
    }

    public string TenantId { get; private set; } = default!;
    public string StripeCustomerId { get; private set; } = default!;

    public static Result<PartnerBillingCustomer> Create(string tenantId, string stripeCustomerId)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || tenantId.Length > TenantIdMaxLength)
        {
            return Result<PartnerBillingCustomer>.Failure(BillingErrors.InvalidTenantId);
        }

        if (string.IsNullOrWhiteSpace(stripeCustomerId) || stripeCustomerId.Length > StripeCustomerIdMaxLength)
        {
            return Result<PartnerBillingCustomer>.Failure(BillingErrors.InvalidStripeCustomerId);
        }

        PartnerBillingCustomer customer = new()
        {
            TenantId = tenantId,
            StripeCustomerId = stripeCustomerId
        };

        return Result<PartnerBillingCustomer>.Success(customer);
    }
}
