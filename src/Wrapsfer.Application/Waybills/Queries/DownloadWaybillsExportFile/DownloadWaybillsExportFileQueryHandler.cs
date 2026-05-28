using Wrapsfer.Application.Abstractions.FileProcessing;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Waybills.Commands.RequestWaybillsExport;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Waybills.Queries.DownloadWaybillsExportFile;

internal sealed class DownloadWaybillsExportFileQueryHandler(
    IWaybillRepository waybillRepository,
    IProductRepository productRepository,
    IPartnerTenantRepository partnerTenantRepository,
    IWaybillReportFileWriter writer) : IQueryHandler<DownloadWaybillsExportFileQuery, DownloadWaybillsExportFileResult>
{
    private const int ExportPageSize = 500;

    public async Task<Result<DownloadWaybillsExportFileResult>> Handle(
        DownloadWaybillsExportFileQuery request,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<string>> tenantIdsResult =
            await ResolveTenantIdsAsync(request, cancellationToken);
        if (tenantIdsResult.IsFailure)
        {
            return Result<DownloadWaybillsExportFileResult>.Failure(tenantIdsResult.Error);
        }

        IReadOnlyList<string> tenantIds = tenantIdsResult.Value;
        List<Waybill> waybills = await LoadWaybillsAsync(request, tenantIds, cancellationToken);

        HashSet<Guid> productIds = waybills
            .SelectMany(w => w.Items)
            .Select(i => i.ProductId)
            .ToHashSet();

        IReadOnlyDictionary<Guid, Product> productsById = productIds.Count == 0
            ? new Dictionary<Guid, Product>()
            : (await productRepository.GetByIdsByTenantIdsAsync(productIds, tenantIds, cancellationToken))
            .ToDictionary(p => p.Id);

        List<WaybillReportRow> rows = waybills.SelectMany(w => ToRows(w, productsById)).ToList();
        byte[] fileBytes = await writer.WriteAsync(rows, WaybillExportFormat.Pdf, cancellationToken);
        string fileName = $"waybills-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.pdf";

        return new DownloadWaybillsExportFileResult(fileBytes, "application/pdf", fileName);
    }

    private async Task<Result<IReadOnlyList<string>>> ResolveTenantIdsAsync(
        DownloadWaybillsExportFileQuery request,
        CancellationToken cancellationToken)
    {
        if (request.PartnerTenantIds is { Count: > 0 } requestedPartnerIds)
        {
            if (!request.IncludeAllPartnerTenants)
            {
                return Result<IReadOnlyList<string>>.Failure(WaybillExportErrors.PartnerScopeNotAllowed);
            }

            IReadOnlyList<PartnerTenant> activePartners =
                await partnerTenantRepository.ListAsync(isActive: true, cancellationToken);
            HashSet<string> activeIds = activePartners
                .Select(p => p.TenantId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            List<string> requested = requestedPartnerIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (requested.Count == 0 || requested.Any(id => !activeIds.Contains(id)))
            {
                return Result<IReadOnlyList<string>>.Failure(WaybillExportErrors.UnknownOrInactivePartner);
            }

            return requested;
        }

        if (request.IncludeAllPartnerTenants)
        {
            IReadOnlyList<PartnerTenant> activePartners =
                await partnerTenantRepository.ListAsync(isActive: true, cancellationToken);
            return activePartners.Select(p => p.TenantId).ToList();
        }

        return Result<IReadOnlyList<string>>.Failure(WaybillExportErrors.PartnerScopeNotAllowed);
    }

    private async Task<List<Waybill>> LoadWaybillsAsync(
        DownloadWaybillsExportFileQuery request,
        IReadOnlyList<string> tenantIds,
        CancellationToken cancellationToken)
    {
        List<Waybill> all = [];
        int page = 1;
        int totalCount;
        do
        {
            (IReadOnlyList<Waybill> items, int total) = await waybillRepository.ListByTenantIdsAsync(
                tenantIds,
                request.From,
                request.To,
                request.Status,
                request.Search,
                page,
                ExportPageSize,
                cancellationToken);
            all.AddRange(items);
            totalCount = total;
            page++;
        } while (all.Count < totalCount);

        return all;
    }

    private static IEnumerable<WaybillReportRow> ToRows(
        Waybill waybill,
        IReadOnlyDictionary<Guid, Product> productsById)
    {
        if (waybill.Items.Count == 0)
        {
            return [ToRow(waybill, null, null)];
        }

        return waybill.Items.Select(item =>
        {
            productsById.TryGetValue(item.ProductId, out Product? product);
            return ToRow(waybill, item, product);
        });
    }

    private static WaybillReportRow ToRow(Waybill waybill, WaybillItem? item, Product? product) => new(
        waybill.TenantId,
        waybill.PackagingDate,
        waybill.WaybillNumber,
        waybill.Status,
        waybill.CancellationReason,
        waybill.PackedAt,
        waybill.HandedOffAt,
        waybill.CancelledAt,
        waybill.CreatedAt,
        waybill.CreatedBy,
        product?.Barcode,
        product?.Name,
        item?.Quantity);
}
