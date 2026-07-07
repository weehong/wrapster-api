using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Billing.Responses;
using Wrapsfer.Application.Billing.StockReportDownloads;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Billing.Queries.GetStockReportDownloadCounts;

internal sealed class GetStockReportDownloadCountsQueryHandler(
    ITenantContext tenantContext,
    IAuditLogRepository auditLogRepository)
    : IQueryHandler<GetStockReportDownloadCountsQuery, IReadOnlyList<StockReportMonthlyDownloadCount>>
{
    public async Task<Result<IReadOnlyList<StockReportMonthlyDownloadCount>>> Handle(
        GetStockReportDownloadCountsQuery request,
        CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        (DateTime fromUtcInclusive, DateTime toUtcExclusive) =
            StockReportDownloadMonthRange.ToUtcRange(request.FromMonth, request.ToMonth);

        IReadOnlyList<StockReportDownloadAudit> downloads =
            await auditLogRepository.GetSuccessfulStockReportDownloadsAsync(
                tenantId, fromUtcInclusive, toUtcExclusive, cancellationToken);

        return Result<IReadOnlyList<StockReportMonthlyDownloadCount>>.Success(
            StockReportDownloadCountsBuilder.Build(tenantId, downloads));
    }
}
