namespace Wrapsfer.Application.Billing;

/// <summary>
/// The billing window and prorated price for a stock report access pass, expressed in UTC
/// instants (for storage and entitlement checks) plus the human-facing local access end date.
/// </summary>
public sealed record StockReportBillingPeriod(
    DateTime ValidFromUtc,
    DateTime ValidToUtc,
    DateOnly AccessEndDate,
    int AmountMinor,
    int DaysRemaining,
    int DaysInMonth);
