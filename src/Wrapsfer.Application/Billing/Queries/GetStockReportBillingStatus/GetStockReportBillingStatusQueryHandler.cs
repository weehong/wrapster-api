using Microsoft.Extensions.Options;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Billing.Queries.GetStockReportBillingStatus;

internal sealed class GetStockReportBillingStatusQueryHandler(
    ITenantContext tenantContext,
    IFeatureEntitlementRepository entitlementRepository,
    StockReportProrationCalculator prorationCalculator,
    IOptions<StripeOptions> stripeOptions)
    : IQueryHandler<GetStockReportBillingStatusQuery, StockReportBillingStatusResult>
{
    public async Task<Result<StockReportBillingStatusResult>> Handle(
        GetStockReportBillingStatusQuery request,
        CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;
        DateTime utcNow = DateTime.UtcNow;
        StripeOptions options = stripeOptions.Value;
        TimeZoneInfo billingTimeZone = TimeZoneInfo.FindSystemTimeZoneById(options.BillingTimeZone);

        FeatureEntitlement? activeEntitlement = await entitlementRepository.GetActiveAsync(
            tenantId, BillingFeature.StockReport, utcNow, cancellationToken);

        if (activeEntitlement is not null)
        {
            (DateOnly accessStartDate, DateOnly accessEndDate) =
                GetActiveAccessDates(activeEntitlement, billingTimeZone);

            return Result<StockReportBillingStatusResult>.Success(
                new StockReportBillingStatusResult(
                    true, accessStartDate, accessEndDate, null, activeEntitlement.Currency));
        }

        StockReportBillingPeriod period = prorationCalculator.Calculate(
            utcNow, billingTimeZone, options.StockReportMonthlyAmountMinor);
        DateOnly quotedAccessStartDate =
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, billingTimeZone));

        return Result<StockReportBillingStatusResult>.Success(
            new StockReportBillingStatusResult(
                false, quotedAccessStartDate, period.AccessEndDate, period.AmountMinor, options.Currency));
    }

    private static (DateOnly StartDate, DateOnly EndDate) GetActiveAccessDates(
        FeatureEntitlement entitlement,
        TimeZoneInfo billingTimeZone)
    {
        // The access window start is the entitlement's effective start, not the row's creation time —
        // CreatedAt would misreport backfilled or re-created entitlements.
        DateOnly accessStartDate = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(entitlement.ValidFromUtc, billingTimeZone));
        DateOnly accessEndDate = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(entitlement.ValidToUtc, billingTimeZone).AddDays(-1));

        return (accessStartDate, accessEndDate);
    }
}
