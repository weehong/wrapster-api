namespace Wrapsfer.Application.Billing.Queries.GetStockReportBillingStatus;

/// <summary>
/// Stock report billing state for the current tenant. When access is inactive,
/// <see cref="AmountMinorDue"/> carries the prorated price to purchase for the remainder of the month.
/// </summary>
public sealed record StockReportBillingStatusResult(
    bool IsActive,
    DateOnly? AccessStartDate,
    DateOnly? AccessEndDate,
    int? AmountMinorDue,
    string Currency);
