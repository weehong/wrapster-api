using Microsoft.Extensions.Options;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Common;
using Wrapsfer.Application.Products;
using Wrapsfer.Application.Waybills.Responses;
using Wrapsfer.Application.Waybills.Services;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;
using TenantSettingsEntity = Wrapsfer.Domain.Entities.TenantSettings;

namespace Wrapsfer.Application.Waybills.Commands.BulkUpdateWaybillStatus;

internal sealed class BulkUpdateWaybillStatusCommandHandler(
    IWaybillRepository waybillRepository,
    ITenantSettingsRepository tenantSettingsRepository,
    StockReservationService stockReservationService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IOptions<ProductSettings> productSettings)
    : ICommandHandler<BulkUpdateWaybillStatusCommand, BulkWaybillStatusUpdateResult>
{
    public async Task<Result<BulkWaybillStatusUpdateResult>> Handle(
        BulkUpdateWaybillStatusCommand request, CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        // Deduplicate requested IDs while preserving request order for reporting.
        List<Guid> orderedIds = [];
        HashSet<Guid> seen = [];
        foreach (Guid id in request.Ids)
        {
            if (seen.Add(id))
            {
                orderedIds.Add(id);
            }
        }

        // Single tenant-scoped batch fetch - never cross-tenant.
        IReadOnlyList<Waybill> waybills =
            await waybillRepository.GetByIdsWithItemsAsync(orderedIds, tenantId, cancellationToken);
        Dictionary<Guid, Waybill> waybillsById = waybills.ToDictionary(w => w.Id);

        int? fallbackThreshold = null;

        List<BulkWaybillStatusUpdateItemResult> results = new(orderedIds.Count);

        foreach (Guid id in orderedIds)
        {
            if (!waybillsById.TryGetValue(id, out Waybill? waybill))
            {
                results.Add(Failed(id, WaybillErrors.NotFound));
                continue;
            }

            if (request.Status == WaybillStatus.HandedOff && fallbackThreshold is null)
            {
                TenantSettingsEntity? settings =
                    await tenantSettingsRepository.GetByTenantIdAsync(tenantId, cancellationToken);
                fallbackThreshold = settings?.DefaultLowStockThreshold
                                    ?? productSettings.Value.GlobalLowStockThreshold;
            }

            Result transitionResult =
                await ApplyTransitionAsync(waybill, request, tenantId, fallbackThreshold ?? 0, cancellationToken);
            if (transitionResult.IsFailure)
            {
                results.Add(Failed(id, transitionResult.Error));
                continue;
            }

            // Persist each successful waybill independently so a later failure cannot undo it.
            await unitOfWork.SaveChangesAsync(cancellationToken);
            results.Add(new BulkWaybillStatusUpdateItemResult(id, true, request.Status, null, null));
        }

        int successCount = results.Count(r => r.Success);
        BulkWaybillStatusUpdateResult result = new(
            results.Count,
            successCount,
            results.Count - successCount,
            results);

        return Result<BulkWaybillStatusUpdateResult>.Success(result);
    }

    private async Task<Result> ApplyTransitionAsync(
        Waybill waybill, BulkUpdateWaybillStatusCommand request, string tenantId, int fallbackThreshold,
        CancellationToken cancellationToken)
    {
        switch (request.Status)
        {
            case WaybillStatus.Packed:
                return waybill.MarkPacked();

            case WaybillStatus.HandedOff:
                {
                    bool isAdmin = tenantContext.Roles.Contains(TenantRoles.Admin, StringComparer.Ordinal);
                    Result markResult = waybill.MarkHandedOff(tenantContext.UserId, isAdmin);
                    if (markResult.IsFailure)
                    {
                        return markResult;
                    }

                    Result consumeResult =
                        await stockReservationService.ConsumeItemsAsync(
                            waybill.Items, tenantId, fallbackThreshold, cancellationToken);
                    if (consumeResult.IsFailure)
                    {
                        // Discard the in-memory transition so a later successful save cannot persist it.
                        waybillRepository.Detach(waybill);
                        return consumeResult;
                    }

                    return Result.Success();
                }

            case WaybillStatus.Cancelled:
                {
                    WaybillStatus previousStatus = waybill.Status;
                    Result cancelResult = waybill.Cancel(request.Reason ?? string.Empty);
                    if (cancelResult.IsFailure)
                    {
                        return cancelResult;
                    }

                    if (previousStatus is WaybillStatus.Draft or WaybillStatus.Packed)
                    {
                        Result releaseResult =
                            await stockReservationService.ReleaseItemsAsync(waybill.Items, tenantId, cancellationToken);
                        if (releaseResult.IsFailure)
                        {
                            // Discard the in-memory transition so a later successful save cannot persist it.
                            waybillRepository.Detach(waybill);
                            return releaseResult;
                        }
                    }

                    return Result.Success();
                }

            default:
                return Result.Failure(WaybillErrors.InvalidStatusTransition);
        }
    }

    private static BulkWaybillStatusUpdateItemResult Failed(Guid id, Error error) =>
        new(id, false, null, error.Code, error.Description);
}
