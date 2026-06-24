namespace Wrapsfer.Application.Billing;

/// <summary>
/// Computes the prorated stock report price and access window for the calendar month that
/// contains a reference instant, using inclusive calendar days in the configured time zone.
/// Example: 23 June through 30 June is 8 inclusive days, so on a MYR 10.00/month plan the
/// prorated amount is round(8 / 30 * 1000) = 267 sen (MYR 2.67).
/// </summary>
public sealed class StockReportProrationCalculator
{
    public StockReportBillingPeriod Calculate(DateTime referenceUtc, TimeZoneInfo billingTimeZone, int monthlyAmountMinor)
    {
        if (monthlyAmountMinor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(monthlyAmountMinor), monthlyAmountMinor,
                "The monthly amount must be greater than zero.");
        }

        DateTime referenceUtcNormalized = referenceUtc.Kind == DateTimeKind.Utc
            ? referenceUtc
            : DateTime.SpecifyKind(referenceUtc, DateTimeKind.Utc);

        DateTime localNow = TimeZoneInfo.ConvertTimeFromUtc(referenceUtcNormalized, billingTimeZone);

        int year = localNow.Year;
        int month = localNow.Month;
        int daysInMonth = DateTime.DaysInMonth(year, month);

        // Inclusive remaining days: from today through the last day of the month.
        int daysRemaining = daysInMonth - localNow.Day + 1;

        DateTime localMonthStart = new(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        DateTime localNextMonthStart = localMonthStart.AddMonths(1);

        DateTime validFromUtc = TimeZoneInfo.ConvertTimeToUtc(localMonthStart, billingTimeZone);
        DateTime validToUtc = TimeZoneInfo.ConvertTimeToUtc(localNextMonthStart, billingTimeZone);

        DateOnly accessEndDate = DateOnly.FromDateTime(localNextMonthStart.AddDays(-1));

        decimal exactAmount = (decimal)daysRemaining / daysInMonth * monthlyAmountMinor;
        int amountMinor = (int)Math.Round(exactAmount, MidpointRounding.AwayFromZero);

        return new StockReportBillingPeriod(
            validFromUtc,
            validToUtc,
            accessEndDate,
            amountMinor,
            daysRemaining,
            daysInMonth);
    }
}
