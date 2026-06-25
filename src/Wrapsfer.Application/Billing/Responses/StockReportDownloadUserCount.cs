namespace Wrapsfer.Application.Billing.Responses;

/// <summary>
/// Successful stock-report download count for a single user within one calendar month.
/// </summary>
public sealed record StockReportDownloadUserCount(
    string? UserId,
    string? Username,
    int SuccessfulDownloadCount);
