using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Infrastructure.Persistence.Repositories;

internal sealed class AuditLogRepository(ApplicationDbContext context) : IAuditLogRepository
{
    // EntityName recorded by AuditBehavior for the stock-report download query (typeof(TRequest).Name),
    // and the audit-metadata key set by DownloadProductStockReportQueryHandler on success.
    private const string StockReportDownloadEntityName = "DownloadProductStockReportQuery";
    private const string SuccessfulDownloadKey = "successfulDownload";

    public async Task<IReadOnlyList<StockReportDownloadAudit>> GetSuccessfulStockReportDownloadsAsync(
        string tenantId,
        DateTime fromUtcInclusive,
        DateTime toUtcExclusive,
        CancellationToken cancellationToken = default)
    {
        // Narrow in SQL by the indexed (EntityName, Action, TenantId, Timestamp) columns, then confirm
        // the successfulDownload flag from the jsonb payload in memory (volume is one row per download).
        List<AuditLog> candidates = await context.AuditLogs
            .AsNoTracking()
            .Where(a => a.EntityName == StockReportDownloadEntityName
                        && a.Action == AuditAction.Executed
                        && a.TenantId == tenantId
                        && a.Timestamp >= fromUtcInclusive
                        && a.Timestamp < toUtcExclusive)
            .OrderBy(a => a.Timestamp)
            .ToListAsync(cancellationToken);

        List<StockReportDownloadAudit> downloads = new();

        foreach (AuditLog candidate in candidates)
        {
            if (IsSuccessfulDownload(candidate.Changes))
            {
                downloads.Add(new StockReportDownloadAudit(
                    candidate.Timestamp, candidate.UserId, candidate.Username));
            }
        }

        return downloads;
    }

    private static bool IsSuccessfulDownload(string? changes)
    {
        if (string.IsNullOrEmpty(changes))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(changes);
            return document.RootElement.TryGetProperty(SuccessfulDownloadKey, out JsonElement flag)
                   && flag.ValueKind == JsonValueKind.True;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
