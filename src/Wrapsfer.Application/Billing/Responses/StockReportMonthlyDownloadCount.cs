namespace Wrapsfer.Application.Billing.Responses;

/// <summary>
/// Successful stock-report download totals for one tenant in one calendar month, broken down by user.
/// </summary>
public sealed record StockReportMonthlyDownloadCount(
    string TenantId,
    string Month,
    int SuccessfulDownloadCount,
    IReadOnlyList<StockReportDownloadUserCount> Users);
