using Microsoft.Extensions.Options;
using Wrapsfer.Application.Billing.Queries.GetStockReportBillingStatus;
using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Application.Billing;

/// <summary>
/// Builds the stock report billing status for a tenant from its active entitlement (if any),
/// quoting the prorated price for the remainder of the month when access is inactive. Shared by
/// the partner-facing status query and the owner-facing billing overview so both stay in sync.
/// </summary>
public sealed class StockReportBillingStatusFactory(
    StockReportProrationCalculator prorationCalculator,
    IOptions<StripeOptions> stripeOptions)
{
    public StockReportBillingStatusResult Build(FeatureEntitlement? activeEntitlement, DateTime utcNow)
    {
        StripeOptions options = stripeOptions.Value;
        TimeZoneInfo billingTimeZone = TimeZoneInfo.FindSystemTimeZoneById(options.BillingTimeZone);

        if (activeEntitlement is not null)
        {
            (DateOnly accessStartDate, DateOnly accessEndDate) =
                GetActiveAccessDates(activeEntitlement, billingTimeZone);

            return new StockReportBillingStatusResult(
                true, accessStartDate, accessEndDate, null, activeEntitlement.Currency);
        }

        StockReportBillingPeriod period = prorationCalculator.Calculate(
            utcNow, billingTimeZone, options.StockReportMonthlyAmountMinor);
        DateOnly quotedAccessStartDate =
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, billingTimeZone));

        return new StockReportBillingStatusResult(
            false, quotedAccessStartDate, period.AccessEndDate, period.AmountMinor, options.Currency);
    }

    private static (DateOnly StartDate, DateOnly EndDate) GetActiveAccessDates(
        FeatureEntitlement entitlement,
        TimeZoneInfo billingTimeZone)
    {
        DateTime accessStartUtc = entitlement.CreatedAt == default
            ? entitlement.ValidFromUtc
            : entitlement.CreatedAt;
        DateOnly accessStartDate = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(accessStartUtc, billingTimeZone));
        DateOnly accessEndDate = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(entitlement.ValidToUtc, billingTimeZone).AddDays(-1));

        return (accessStartDate, accessEndDate);
    }
}
