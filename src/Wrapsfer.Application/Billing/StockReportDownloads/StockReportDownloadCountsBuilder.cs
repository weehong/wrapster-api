using System.Globalization;
using Wrapsfer.Application.Billing.Responses;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Billing.StockReportDownloads;

/// <summary>
/// Groups successful stock-report downloads into per-month, per-user counts for a single tenant.
/// Months are bucketed by the UTC calendar month of the audit timestamp.
/// </summary>
internal static class StockReportDownloadCountsBuilder
{
    public static IReadOnlyList<StockReportMonthlyDownloadCount> Build(
        string tenantId,
        IReadOnlyList<StockReportDownloadAudit> downloads) =>
        downloads
            .GroupBy(download => new DateOnly(download.TimestampUtc.Year, download.TimestampUtc.Month, 1))
            .OrderBy(monthGroup => monthGroup.Key)
            .Select(monthGroup => new StockReportMonthlyDownloadCount(
                tenantId,
                monthGroup.Key.ToString(StockReportDownloadMonthRange.MonthFormat, CultureInfo.InvariantCulture),
                monthGroup.Count(),
                monthGroup
                    .GroupBy(download => new { download.UserId, download.Username })
                    .OrderBy(userGroup => userGroup.Key.Username)
                    .Select(userGroup => new StockReportDownloadUserCount(
                        userGroup.Key.UserId,
                        userGroup.Key.Username,
                        userGroup.Count()))
                    .ToList()))
            .ToList();
}
