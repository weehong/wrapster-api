using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Billing.Responses;

namespace Wrapsfer.Application.Billing.Queries.GetStockReportDownloadCounts;

/// <summary>
/// Partner/self query for monthly successful stock-report download usage. Scoped to the caller's
/// tenant (resolved from the JWT); months are inclusive <c>YYYY-MM</c> bounds.
/// </summary>
public sealed record GetStockReportDownloadCountsQuery(string FromMonth, string ToMonth)
    : IQuery<IReadOnlyList<StockReportMonthlyDownloadCount>>;
