using Microsoft.Extensions.Options;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Billing.Abstractions;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Billing.Commands.CreateStockReportCheckout;

internal sealed class CreateStockReportCheckoutCommandHandler(
    ITenantContext tenantContext,
    StockReportProrationCalculator prorationCalculator,
    StripeCustomerProvisioner customerProvisioner,
    IStripeBillingGateway billingGateway,
    IFeatureEntitlementRepository entitlementRepository,
    IUnitOfWork unitOfWork,
    IOptions<StripeOptions> stripeOptions)
    : ICommandHandler<CreateStockReportCheckoutCommand, CreateStockReportCheckoutResult>
{
    public async Task<Result<CreateStockReportCheckoutResult>> Handle(
        CreateStockReportCheckoutCommand request,
        CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;
        DateTime utcNow = DateTime.UtcNow;
        StripeOptions options = stripeOptions.Value;

        FeatureEntitlement? activeEntitlement = await entitlementRepository.GetActiveAsync(
            tenantId, BillingFeature.StockReport, utcNow, cancellationToken);
        if (activeEntitlement is not null)
        {
            return Result<CreateStockReportCheckoutResult>.Failure(BillingErrors.StockReportAlreadyActive);
        }

        TimeZoneInfo billingTimeZone = TimeZoneInfo.FindSystemTimeZoneById(options.BillingTimeZone);
        StockReportBillingPeriod period = prorationCalculator.Calculate(
            utcNow, billingTimeZone, options.StockReportMonthlyAmountMinor);

        Result<string> customerResult = await customerProvisioner.EnsureCustomerIdAsync(tenantId, cancellationToken);
        if (customerResult.IsFailure)
        {
            return Result<CreateStockReportCheckoutResult>.Failure(customerResult.Error);
        }

        Dictionary<string, string> metadata = new()
        {
            [BillingMetadataKeys.TenantId] = tenantId,
            [BillingMetadataKeys.Feature] = BillingFeature.StockReport.ToString()
        };

        StripeCheckoutSessionRequest sessionRequest = new(
            customerResult.Value,
            options.Currency,
            period.AmountMinor,
            $"Stock report access — {period.AccessEndDate:yyyy-MM}",
            BillingUrlBuilder.Combine(options.FrontendBaseUrl, options.CheckoutSuccessPath),
            BillingUrlBuilder.Combine(options.FrontendBaseUrl, options.CheckoutCancelPath),
            metadata,
            // Per-attempt key: makes the SDK's transport-level retries reuse one session while still
            // letting a fresh user attempt create a new one. Business-level "one active pass per
            // month" is enforced by the entitlement exclusion constraint, not this key.
            Guid.NewGuid().ToString("N"));

        StripeCheckoutSessionResult session =
            await billingGateway.CreateCheckoutSessionAsync(sessionRequest, cancellationToken);

        Result<FeatureEntitlement> entitlementResult = FeatureEntitlement.CreatePending(
            tenantId,
            BillingFeature.StockReport,
            period.ValidFromUtc,
            period.ValidToUtc,
            period.AmountMinor,
            options.Currency,
            session.SessionId);
        if (entitlementResult.IsFailure)
        {
            return Result<CreateStockReportCheckoutResult>.Failure(entitlementResult.Error);
        }

        entitlementRepository.Add(entitlementResult.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CreateStockReportCheckoutResult>.Success(
            new CreateStockReportCheckoutResult(session.Url));
    }
}
