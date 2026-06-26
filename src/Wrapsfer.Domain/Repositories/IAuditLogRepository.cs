using Wrapsfer.Domain.Common;

namespace Wrapsfer.Domain.Repositories;

public interface IAuditLogRepository
{
    Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> ListForEntityAsync(
        string entityName,
        string entityId,
        string tenantId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every successful stock-report download recorded for the tenant whose audit timestamp
    /// falls in the half-open UTC range <c>[fromUtcInclusive, toUtcExclusive)</c>. Payment-required
    /// and otherwise failed attempts are excluded so the result counts billable usage only.
    /// </summary>
    Task<IReadOnlyList<StockReportDownloadAudit>> GetSuccessfulStockReportDownloadsAsync(
        string tenantId,
        DateTime fromUtcInclusive,
        DateTime toUtcExclusive,
        CancellationToken cancellationToken = default);
}
