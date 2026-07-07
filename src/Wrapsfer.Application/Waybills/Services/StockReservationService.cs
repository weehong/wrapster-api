using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Waybills.Services;

public sealed class StockReservationService(
    IProductRepository productRepository,
    IProductComponentRepository productComponentRepository)
{
    public Task<Result> ReserveAsync(Guid productId, int quantity, string tenantId,
        CancellationToken cancellationToken) =>
        ApplyAsync(new Dictionary<Guid, int> { [productId] = quantity }, tenantId, ReservationOperation.Reserve,
            0, cancellationToken);

    public Task<Result> ReleaseAsync(Guid productId, int quantity, string tenantId,
        CancellationToken cancellationToken) =>
        ApplyAsync(new Dictionary<Guid, int> { [productId] = quantity }, tenantId, ReservationOperation.Release,
            0, cancellationToken);

    public Task<Result> ReserveItemsAsync(IEnumerable<WaybillItem> items, string tenantId,
        CancellationToken cancellationToken) =>
        AggregateAndApplyAsync(items, tenantId, ReservationOperation.Reserve, 0, cancellationToken);

    public Task<Result> ReleaseItemsAsync(IEnumerable<WaybillItem> items, string tenantId,
        CancellationToken cancellationToken) =>
        AggregateAndApplyAsync(items, tenantId, ReservationOperation.Release, 0, cancellationToken);

    public Task<Result> ConsumeItemsAsync(IEnumerable<WaybillItem> items, string tenantId,
        int fallbackThreshold, CancellationToken cancellationToken) =>
        AggregateAndApplyAsync(items, tenantId, ReservationOperation.Consume, fallbackThreshold, cancellationToken);

    private async Task<Result> AggregateAndApplyAsync(IEnumerable<WaybillItem> items, string tenantId,
        ReservationOperation operation, int fallbackThreshold, CancellationToken cancellationToken)
    {
        Dictionary<Guid, int> totalsByProduct = new();
        foreach (WaybillItem item in items)
        {
            if (totalsByProduct.TryGetValue(item.ProductId, out int existing))
            {
                totalsByProduct[item.ProductId] = existing + item.Quantity;
            }
            else
            {
                totalsByProduct[item.ProductId] = item.Quantity;
            }
        }

        return await ApplyAsync(totalsByProduct, tenantId, operation, fallbackThreshold, cancellationToken);
    }

    private async Task<Result> ApplyAsync(Dictionary<Guid, int> productDemand, string tenantId,
        ReservationOperation operation, int fallbackThreshold, CancellationToken cancellationToken)
    {
        if (productDemand.Count == 0)
        {
            return Result.Success();
        }

        IReadOnlyList<Product> products =
            await productRepository.GetByIdsAsync(productDemand.Keys, tenantId, cancellationToken);
        Dictionary<Guid, Product> productsById = products.ToDictionary(p => p.Id);

        Dictionary<Guid, int> leafDemand = new();

        foreach ((Guid productId, int quantity) in productDemand)
        {
            if (!productsById.TryGetValue(productId, out Product? product))
            {
                return Result.Failure(ProductErrors.NotFound);
            }

            if (product.Type == ProductType.Bundle)
            {
                IReadOnlyList<ProductComponent> components =
                    await productComponentRepository.GetByParentIdAsync(productId, tenantId, cancellationToken);

                if (components.Count == 0)
                {
                    return Result.Failure(ProductErrors.BundleHasNoComponents);
                }

                foreach (ProductComponent component in components)
                {
                    int componentDemand = component.Quantity * quantity;
                    AccumulateDemand(leafDemand, component.ChildProductId, componentDemand);
                }
            }
            else
            {
                AccumulateDemand(leafDemand, productId, quantity);
            }
        }

        HashSet<Guid> missingLeafIds = leafDemand.Keys.Where(id => !productsById.ContainsKey(id)).ToHashSet();
        if (missingLeafIds.Count > 0)
        {
            IReadOnlyList<Product> leafProducts =
                await productRepository.GetByIdsAsync(missingLeafIds, tenantId, cancellationToken);

            if (leafProducts.Count != missingLeafIds.Count)
            {
                return Result.Failure(ProductErrors.NotFound);
            }

            foreach (Product leaf in leafProducts)
            {
                productsById[leaf.Id] = leaf;
            }
        }

        foreach ((Guid productId, int quantity) in leafDemand)
        {
            if (!productsById.TryGetValue(productId, out Product? leaf))
            {
                return Result.Failure(ProductErrors.NotFound);
            }

            Result canResult = operation switch
            {
                ReservationOperation.Reserve => leaf.CanReserve(quantity),
                ReservationOperation.Release => leaf.CanRelease(quantity),
                ReservationOperation.Consume => leaf.CanConsume(quantity),
                _ => Result.Failure(Error.Failure)
            };

            if (canResult.IsFailure)
            {
                return canResult;
            }
        }

        foreach ((Guid productId, int quantity) in leafDemand)
        {
            Product leaf = productsById[productId];

            Result result = operation switch
            {
                ReservationOperation.Reserve => leaf.Reserve(quantity),
                ReservationOperation.Release => leaf.Release(quantity),
                ReservationOperation.Consume => leaf.Consume(quantity, fallbackThreshold),
                _ => Result.Failure(Error.Failure)
            };

            if (result.IsFailure)
            {
                return result;
            }
        }

        return Result.Success();
    }

    private static void AccumulateDemand(Dictionary<Guid, int> demand, Guid productId, int quantity)
    {
        if (demand.TryGetValue(productId, out int existing))
        {
            demand[productId] = existing + quantity;
        }
        else
        {
            demand[productId] = quantity;
        }
    }
}
