using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Billing.Responses;

namespace Wrapsfer.Application.Billing.Queries.GetPartnerStockReportDownloadCounts;

/// <summary>
/// Owner/admin query for a specific partner tenant's monthly successful stock-report download usage.
/// Months are inclusive <c>YYYY-MM</c> bounds.
/// </summary>
public sealed record GetPartnerStockReportDownloadCountsQuery(string TenantId, string FromMonth, string ToMonth)
    : IQuery<IReadOnlyList<StockReportMonthlyDownloadCount>>;
