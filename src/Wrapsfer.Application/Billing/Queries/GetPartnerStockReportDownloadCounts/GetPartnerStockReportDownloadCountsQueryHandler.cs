using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Billing.Responses;
using Wrapsfer.Application.Billing.StockReportDownloads;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Billing.Queries.GetPartnerStockReportDownloadCounts;

internal sealed class GetPartnerStockReportDownloadCountsQueryHandler(
    IAuditLogRepository auditLogRepository)
    : IQueryHandler<GetPartnerStockReportDownloadCountsQuery, IReadOnlyList<StockReportMonthlyDownloadCount>>
{
    public async Task<Result<IReadOnlyList<StockReportMonthlyDownloadCount>>> Handle(
        GetPartnerStockReportDownloadCountsQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId))
        {
            return Result<IReadOnlyList<StockReportMonthlyDownloadCount>>.Failure(BillingErrors.InvalidTenantId);
        }

        string tenantId = request.TenantId.Trim();

        (DateTime fromUtcInclusive, DateTime toUtcExclusive) =
            StockReportDownloadMonthRange.ToUtcRange(request.FromMonth, request.ToMonth);

        IReadOnlyList<StockReportDownloadAudit> downloads =
            await auditLogRepository.GetSuccessfulStockReportDownloadsAsync(
                tenantId, fromUtcInclusive, toUtcExclusive, cancellationToken);

        return Result<IReadOnlyList<StockReportMonthlyDownloadCount>>.Success(
            StockReportDownloadCountsBuilder.Build(tenantId, downloads));
    }
}
