using System.Globalization;

namespace Wrapsfer.Application.Billing.StockReportDownloads;

/// <summary>
/// Parsing and range helpers for the <c>YYYY-MM</c> month parameters used by the stock-report
/// download usage endpoints. Shared by the partner/self and owner/admin queries and validators.
/// </summary>
internal static class StockReportDownloadMonthRange
{
    public const string MonthFormat = "yyyy-MM";

    public static bool TryParseMonth(string? value, out DateOnly firstOfMonth)
    {
        firstOfMonth = default;

        if (string.IsNullOrWhiteSpace(value)
            || !DateOnly.TryParseExact(
                value, MonthFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly parsed))
        {
            return false;
        }

        firstOfMonth = new DateOnly(parsed.Year, parsed.Month, 1);
        return true;
    }

    public static bool IsValidMonth(string? value) => TryParseMonth(value, out _);

    public static bool IsOrderedRange(string? fromMonth, string? toMonth) =>
        TryParseMonth(fromMonth, out DateOnly from)
        && TryParseMonth(toMonth, out DateOnly to)
        && from <= to;

    /// <summary>
    /// Converts inclusive <c>from</c>/<c>to</c> months into a half-open UTC range
    /// <c>[fromMonthStart, monthAfterToStart)</c>. Callers must validate the inputs first.
    /// </summary>
    public static (DateTime FromUtcInclusive, DateTime ToUtcExclusive) ToUtcRange(string fromMonth, string toMonth)
    {
        if (!TryParseMonth(fromMonth, out DateOnly from))
        {
            throw new ArgumentException($"'{fromMonth}' is not a valid {MonthFormat} month.", nameof(fromMonth));
        }

        if (!TryParseMonth(toMonth, out DateOnly to))
        {
            throw new ArgumentException($"'{toMonth}' is not a valid {MonthFormat} month.", nameof(toMonth));
        }

        DateTime fromUtcInclusive = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        DateTime toUtcExclusive = to.AddMonths(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        return (fromUtcInclusive, toUtcExclusive);
    }
}
