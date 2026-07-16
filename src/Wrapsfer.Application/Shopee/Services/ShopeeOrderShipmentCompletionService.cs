using Microsoft.Extensions.Logging;
using Wrapsfer.Application.Waybills.Services;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Shopee.Services;

/// <summary>
/// Completes an arranged Shopee shipment once the tracking number is known: assigns the
/// tracking number, creates the Wrapsfer waybill (waybill number = tracking number,
/// packaging date = current UTC date), reserves stock, and links the two. Idempotent per
/// order — a Shipped order is left untouched.
/// </summary>
public sealed class ShopeeOrderShipmentCompletionService(
    IWaybillRepository waybillRepository,
    IProductRepository productRepository,
    StockReservationService stockReservationService,
    IUnitOfWork unitOfWork,
    ILogger<ShopeeOrderShipmentCompletionService> logger)
{
    public async Task<Result> CompleteAsync(
        ShopeeOrder order, string trackingNumber, CancellationToken cancellationToken)
    {
        if (order.Status == ShopeeOrderStatus.Shipped)
        {
            return Result.Success();
        }

        if (order.Status != ShopeeOrderStatus.AwaitingTracking)
        {
            return Result.Failure(ShopeeOrderErrors.NotAwaitingTracking);
        }

        if (order.HasUnresolvedItems)
        {
            return await FailAsync(order, ShopeeOrderErrors.ItemsNotLinked, cancellationToken);
        }

        bool numberTaken = await waybillRepository.ExistsByNumberAsync(
            trackingNumber, order.TenantId, cancellationToken);
        if (numberTaken)
        {
            return await FailAsync(order, ShopeeOrderErrors.TrackingNumberConflict, cancellationToken);
        }

        Result assignResult = order.AssignTracking(trackingNumber);
        if (assignResult.IsFailure)
        {
            return assignResult;
        }

        IReadOnlyList<Product> products = await productRepository.GetByIdsAsync(
            order.Items.Select(i => i.ProductId!.Value), order.TenantId, cancellationToken);
        Dictionary<Guid, Product> productsById = products.ToDictionary(p => p.Id);

        Result<Waybill> waybillResult = Waybill.Create(
            order.TenantId, DateOnly.FromDateTime(DateTime.UtcNow), order.TrackingNumber!);
        if (waybillResult.IsFailure)
        {
            return await FailAsync(order, waybillResult.Error, cancellationToken);
        }

        Waybill waybill = waybillResult.Value;
        foreach (ShopeeOrderItem item in order.Items)
        {
            if (!productsById.TryGetValue(item.ProductId!.Value, out Product? product))
            {
                return await FailAsync(order, ShopeeOrderErrors.ProductNotFoundForItem, cancellationToken);
            }

            Result<WaybillItem> addResult = waybill.AddOrIncrementItem(
                product.Id, product.Barcode, item.Quantity);
            if (addResult.IsFailure)
            {
                return await FailAsync(order, addResult.Error, cancellationToken);
            }
        }

        Result reserveResult = await stockReservationService.ReserveItemsAsync(
            waybill.Items, order.TenantId, cancellationToken);
        if (reserveResult.IsFailure)
        {
            return await FailAsync(order, reserveResult.Error, cancellationToken);
        }

        waybillRepository.Add(waybill);
        Result linkResult = order.LinkWaybill(waybill.Id);
        if (linkResult.IsFailure)
        {
            return linkResult;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Shopee order {OrderSn} completed: waybill {WaybillNumber} created for tenant {TenantId}",
            order.OrderSn, waybill.WaybillNumber, order.TenantId);
        return Result.Success();
    }

    private async Task<Result> FailAsync(ShopeeOrder order, Error error, CancellationToken cancellationToken)
    {
        Result failResult = order.MarkShipmentFailed(error.Description, DateTime.UtcNow);
        if (failResult.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Failure(error);
    }
}
