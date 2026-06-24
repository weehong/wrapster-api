using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Domain.Entities;

/// <summary>
/// A tenant's paid access to a billable feature for a fixed validity window. For the stock
/// report this is a one-time monthly access pass: created as <see cref="FeatureEntitlementStatus.PendingPayment"/>
/// at checkout and flipped to <see cref="FeatureEntitlementStatus.Active"/> once the Stripe
/// webhook confirms payment. Access is granted only while <see cref="IsActiveAt"/> is true.
/// </summary>
public sealed class FeatureEntitlement : AuditableEntity
{
    private const int TenantIdMaxLength = 256;
    private const int CurrencyMaxLength = 8;
    private const int StripeIdMaxLength = 256;

    private FeatureEntitlement()
    {
    }

    public string TenantId { get; private set; } = default!;
    public BillingFeature Feature { get; private set; }
    public FeatureEntitlementStatus Status { get; private set; }
    public DateTime ValidFromUtc { get; private set; }
    public DateTime ValidToUtc { get; private set; }
    public int AmountMinor { get; private set; }
    public string Currency { get; private set; } = default!;
    public string StripeCheckoutSessionId { get; private set; } = default!;
    public string? StripePaymentIntentId { get; private set; }
    public string? StripeInvoiceId { get; private set; }

    public static Result<FeatureEntitlement> CreatePending(
        string tenantId,
        BillingFeature feature,
        DateTime validFromUtc,
        DateTime validToUtc,
        int amountMinor,
        string currency,
        string stripeCheckoutSessionId)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || tenantId.Length > TenantIdMaxLength)
        {
            return Result<FeatureEntitlement>.Failure(BillingErrors.InvalidTenantId);
        }

        if (validToUtc <= validFromUtc)
        {
            return Result<FeatureEntitlement>.Failure(BillingErrors.InvalidValidityRange);
        }

        if (amountMinor <= 0)
        {
            return Result<FeatureEntitlement>.Failure(BillingErrors.InvalidAmount);
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Length > CurrencyMaxLength)
        {
            return Result<FeatureEntitlement>.Failure(BillingErrors.InvalidCurrency);
        }

        if (string.IsNullOrWhiteSpace(stripeCheckoutSessionId) || stripeCheckoutSessionId.Length > StripeIdMaxLength)
        {
            return Result<FeatureEntitlement>.Failure(BillingErrors.InvalidCheckoutSessionId);
        }

        FeatureEntitlement entitlement = new()
        {
            TenantId = tenantId,
            Feature = feature,
            Status = FeatureEntitlementStatus.PendingPayment,
            ValidFromUtc = validFromUtc,
            ValidToUtc = validToUtc,
            AmountMinor = amountMinor,
            Currency = currency,
            StripeCheckoutSessionId = stripeCheckoutSessionId
        };

        return Result<FeatureEntitlement>.Success(entitlement);
    }

    /// <summary>
    /// Confirms payment and grants access. Idempotent: re-applying for an already-active
    /// entitlement is a no-op so duplicate webhook deliveries cannot corrupt billing state.
    /// </summary>
    public Result Activate(string? stripePaymentIntentId, string? stripeInvoiceId)
    {
        if (Status == FeatureEntitlementStatus.Active)
        {
            return Result.Success();
        }

        Status = FeatureEntitlementStatus.Active;
        StripePaymentIntentId = stripePaymentIntentId;
        StripeInvoiceId = stripeInvoiceId;
        return Result.Success();
    }

    public Result Cancel()
    {
        if (Status == FeatureEntitlementStatus.Canceled)
        {
            return Result.Success();
        }

        Status = FeatureEntitlementStatus.Canceled;
        return Result.Success();
    }

    public bool IsActiveAt(DateTime utcNow) =>
        Status == FeatureEntitlementStatus.Active
        && utcNow >= ValidFromUtc
        && utcNow < ValidToUtc;
}
