using Microsoft.Extensions.Logging;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Shopee.Services;

/// <summary>
/// Fetches a Shopee order's detail and upserts the local ShopeeOrder, resolving each
/// line against the tenant's product links. Shared by the webhook consumer, the
/// reconciliation job, and the on-demand relink command.
/// </summary>
public sealed class ShopeeOrderIngestionService(
    IShopeeGateway shopeeGateway,
    IShopeeOrderRepository orderRepository,
    IShopeeProductLinkRepository linkRepository,
    IFulfillmentDelegationRepository delegationRepository,
    IProductRepository productRepository,
    ShopeeOrderCancellationService cancellationService,
    IUnitOfWork unitOfWork,
    ILogger<ShopeeOrderIngestionService> logger)
{
    private const string StatusUnpaid = "UNPAID";
    private const string StatusCancelled = "CANCELLED";
    private const string AutoMatchActor = "system:auto-match";

    public async Task<Result> IngestOrderAsync(
        ShopeeShopConnection connection, string orderSn, CancellationToken cancellationToken)
    {
        Result<ShopeeOrderDetail> detailResult = await shopeeGateway.GetOrderDetailAsync(
            connection.ShopId, connection.AccessToken, orderSn, cancellationToken);
        if (detailResult.IsFailure)
        {
            return Result.Failure(detailResult.Error);
        }

        ShopeeOrderDetail detail = detailResult.Value;
        ShopeeOrder? order = await orderRepository.GetByOrderSnAsync(
            orderSn, connection.TenantId, cancellationToken);

        IReadOnlyList<ShopeeOrderItemSnapshot> items =
            await ResolveItemsAsync(connection.TenantId, detail.Items, cancellationToken);
        ShopeeOrderSnapshot snapshot = ToSnapshot(detail);

        if (order is null)
        {
            if (detail.Status is StatusUnpaid or StatusCancelled)
            {
                return Result.Success();
            }

            Result<ShopeeOrder> createResult = ShopeeOrder.Create(
                connection.TenantId, detail.OrderSn, detail.Region ?? connection.Region,
                snapshot, items, DateTime.UtcNow);
            if (createResult.IsFailure)
            {
                logger.LogWarning(
                    "Shopee order {OrderSn} for tenant {TenantId} could not be created: {ErrorCode}",
                    orderSn, connection.TenantId, createResult.Error.Code);
                return Result.Failure(createResult.Error);
            }

            orderRepository.Add(createResult.Value);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        Result applyResult = order.ApplyShopeeSnapshot(snapshot, items, DateTime.UtcNow);
        if (applyResult.IsFailure)
        {
            unitOfWork.ClearChangeTracker();
            return applyResult;
        }

        if (detail.Status == StatusCancelled && order.Status != ShopeeOrderStatus.Cancelled)
        {
            Result cancellationResult =
                await cancellationService.HandleCancellationAsync(order, cancellationToken);
            if (cancellationResult.IsFailure)
            {
                unitOfWork.ClearChangeTracker();
                return cancellationResult;
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<IReadOnlyList<ShopeeOrderItemSnapshot>> ResolveItemsAsync(
        string tenantId,
        IReadOnlyList<ShopeeOrderDetailItem> items,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ShopeeProductLink> links =
            await linkRepository.ListByTenantAsync(tenantId, cancellationToken);
        Dictionary<(long ItemId, long ModelId), Guid> productByUnit = links.ToDictionary(
            l => (l.ShopeeItemId, l.ShopeeModelId), l => l.ProductId);

        List<ShopeeOrderDetailItem> unresolved = items
            .Where(i => !productByUnit.ContainsKey((i.ItemId, i.ModelId)))
            .ToList();
        if (unresolved.Count > 0)
        {
            Dictionary<(long ItemId, long ModelId), Guid> autoMatched =
                await AutoMatchBySkuAsync(tenantId, unresolved, cancellationToken);
            foreach (KeyValuePair<(long ItemId, long ModelId), Guid> match in autoMatched)
            {
                productByUnit[match.Key] = match.Value;
            }
        }

        return items
            .Select(i => new ShopeeOrderItemSnapshot(
                i.ItemId,
                i.ModelId,
                i.ItemName,
                i.ModelName,
                i.ItemSku,
                i.Quantity,
                productByUnit.TryGetValue((i.ItemId, i.ModelId), out Guid productId)
                    ? productId
                    : null))
            .ToList();
    }

    /// <summary>
    /// For tenants with an Active fulfillment delegation, unlinked order lines whose Shopee
    /// SKU exactly matches a product barcode (ordinal, trimmed) are linked automatically.
    /// The created ShopeeProductLink persists with the order in the caller's SaveChanges,
    /// so future orders for the same item resolve from the link table directly.
    /// </summary>
    private async Task<Dictionary<(long ItemId, long ModelId), Guid>> AutoMatchBySkuAsync(
        string tenantId,
        IReadOnlyList<ShopeeOrderDetailItem> unresolved,
        CancellationToken cancellationToken)
    {
        Dictionary<(long ItemId, long ModelId), Guid> matches = [];
        FulfillmentDelegation? delegation =
            await delegationRepository.GetByTenantIdAsync(tenantId, cancellationToken);
        if (delegation is null || delegation.Status != FulfillmentDelegationStatus.Active)
        {
            return matches;
        }

        List<string> skus = unresolved
            .Select(i => i.ItemSku?.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (skus.Count == 0)
        {
            return matches;
        }

        IReadOnlyList<Product> products =
            await productRepository.GetByBarcodesAsync(skus, tenantId, cancellationToken);
        Dictionary<string, Guid> productByBarcode = products.ToDictionary(
            p => p.Barcode, p => p.Id, StringComparer.Ordinal);

        foreach (ShopeeOrderDetailItem item in unresolved)
        {
            string? sku = item.ItemSku?.Trim();
            if (string.IsNullOrEmpty(sku)
                || matches.ContainsKey((item.ItemId, item.ModelId))
                || !productByBarcode.TryGetValue(sku, out Guid productId))
            {
                continue;
            }

            Result<ShopeeProductLink> linkResult = ShopeeProductLink.Create(
                tenantId, productId, item.ItemId, item.ModelId,
                item.ItemName, item.ModelName, item.ItemSku, AutoMatchActor);
            if (linkResult.IsFailure)
            {
                logger.LogWarning(
                    "Auto-match link creation failed for tenant {TenantId} item {ItemId}/{ModelId}: {ErrorCode}",
                    tenantId, item.ItemId, item.ModelId, linkResult.Error.Code);
                continue;
            }

            linkRepository.Add(linkResult.Value);
            matches[(item.ItemId, item.ModelId)] = productId;
        }

        return matches;
    }

    private static ShopeeOrderSnapshot ToSnapshot(ShopeeOrderDetail detail) => new(
        detail.Status,
        detail.BuyerUsername,
        detail.RecipientName,
        detail.RecipientPhone,
        detail.RecipientAddress,
        detail.TotalAmount,
        detail.Currency,
        detail.CodAmount,
        detail.ShippingCarrier,
        detail.ShipByDate);
}
