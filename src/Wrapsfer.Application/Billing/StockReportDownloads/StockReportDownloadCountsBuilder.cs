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
                    // Group on the stable user identity (UserId) so a username change does not split
                    // one person into multiple buckets; fall back to Username only when there is no
                    // UserId. Username is carried through as display data from the latest download.
                    .GroupBy(download => download.UserId is not null
                        ? (UserId: download.UserId, UsernameKey: (string?)null)
                        : (UserId: (string?)null, UsernameKey: download.Username))
                    .Select(userGroup =>
                    {
                        StockReportDownloadAudit latest = userGroup
                            .OrderByDescending(download => download.TimestampUtc)
                            .First();
                        return new StockReportDownloadUserCount(
                            latest.UserId,
                            latest.Username,
                            userGroup.Count());
                    })
                    .OrderBy(user => user.Username)
                    .ToList()))
            .ToList();
}
