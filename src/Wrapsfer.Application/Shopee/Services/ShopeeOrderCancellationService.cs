using Microsoft.Extensions.Logging;
using Wrapsfer.Application.Waybills.Services;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Shopee.Services;

/// <summary>
/// Applies a Shopee-side cancellation to the local order and, when the linked waybill is
/// still cancellable (Draft/Packed), cancels it and releases the reserved stock — the same
/// effect as a manual waybill cancellation. HandedOff waybills are never touched; the
/// cancelled order itself is the attention flag. Callers own SaveChanges.
/// </summary>
public sealed class ShopeeOrderCancellationService(
    IWaybillRepository waybillRepository,
    StockReservationService stockReservationService,
    ILogger<ShopeeOrderCancellationService> logger)
{
    public const string CancellationReason = "Shopee order cancelled";

    public async Task<Result> HandleCancellationAsync(ShopeeOrder order, CancellationToken cancellationToken)
    {
        Result markResult = order.MarkCancelled(DateTime.UtcNow);
        if (markResult.IsFailure)
        {
            return markResult;
        }

        if (order.WaybillId is null)
        {
            return Result.Success();
        }

        Waybill? waybill = await waybillRepository.GetByIdWithItemsAsync(
            order.WaybillId.Value, order.TenantId, cancellationToken);
        if (waybill is null
            || waybill.Status is WaybillStatus.HandedOff or WaybillStatus.Cancelled)
        {
            logger.LogInformation(
                "Shopee order {OrderSn} cancelled but waybill {WaybillId} is not cancellable",
                order.OrderSn, order.WaybillId);
            return Result.Success();
        }

        WaybillStatus previousStatus = waybill.Status;
        Result cancelResult = waybill.Cancel(CancellationReason);
        if (cancelResult.IsFailure)
        {
            return cancelResult;
        }

        if (previousStatus is WaybillStatus.Draft or WaybillStatus.Packed)
        {
            Result releaseResult = await stockReservationService.ReleaseItemsAsync(
                waybill.Items, order.TenantId, cancellationToken);
            if (releaseResult.IsFailure)
            {
                return releaseResult;
            }
        }

        return Result.Success();
    }
}
