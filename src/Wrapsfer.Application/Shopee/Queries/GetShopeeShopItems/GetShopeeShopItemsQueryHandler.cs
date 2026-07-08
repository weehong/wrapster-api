using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Application.Shopee.Common;
using Wrapsfer.Application.Shopee.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Shopee.Queries.GetShopeeShopItems;

internal sealed class GetShopeeShopItemsQueryHandler(
    IShopeeShopConnectionRepository connectionRepository,
    IShopeeProductLinkRepository linkRepository,
    IProductRepository productRepository,
    IShopeeGateway shopeeGateway)
    : IQueryHandler<GetShopeeShopItemsQuery, ShopeeShopItemsResponse>
{
    public async Task<Result<ShopeeShopItemsResponse>> Handle(
        GetShopeeShopItemsQuery request,
        CancellationToken cancellationToken)
    {
        ShopeeShopConnection? connection =
            await connectionRepository.GetByTenantIdAsync(request.TenantId, cancellationToken);
        if (connection is null)
        {
            return Result<ShopeeShopItemsResponse>.Failure(ShopeeProductLinkErrors.ConnectionNotFound);
        }

        Result<ShopeeItemPage> pageResult = await shopeeGateway.GetItemListAsync(
            connection.ShopId, connection.AccessToken, request.Offset, request.PageSize, cancellationToken);
        if (pageResult.IsFailure)
        {
            return Result<ShopeeShopItemsResponse>.Failure(ShopeeProductLinkErrors.ItemFetchFailed);
        }

        ShopeeItemPage page = pageResult.Value;
        if (page.ItemIds.Count == 0)
        {
            return Result<ShopeeShopItemsResponse>.Success(
                new ShopeeShopItemsResponse([], page.HasNextPage, page.NextOffset, page.TotalCount));
        }

        Result<IReadOnlyList<ShopeeItemDetail>> detailResult = await shopeeGateway.GetItemBaseInfoAsync(
            connection.ShopId, connection.AccessToken, page.ItemIds, cancellationToken);
        if (detailResult.IsFailure)
        {
            return Result<ShopeeShopItemsResponse>.Failure(ShopeeProductLinkErrors.ItemFetchFailed);
        }

        IReadOnlyList<ShopeeProductLink> links = await linkRepository.ListByShopeeItemIdsAsync(
            request.TenantId, page.ItemIds, cancellationToken);
        IReadOnlyList<Product> products = await productRepository.GetByIdsAsync(
            links.Select(l => l.ProductId), request.TenantId, cancellationToken);
        Dictionary<Guid, Product> productsById = products.ToDictionary(p => p.Id);
        Dictionary<string, ShopeeProductLinkResponse> linksByUnit = links
            .Where(l => productsById.ContainsKey(l.ProductId))
            .ToDictionary(
                l => UnitKey(l.ShopeeItemId, l.ShopeeModelId),
                l => ShopeeProductLinkResponseMapper.Map(l, productsById[l.ProductId]));

        List<ShopeeShopItemResponse> items = [];
        foreach (ShopeeItemDetail item in detailResult.Value)
        {
            IReadOnlyList<ShopeeShopItemModelResponse> models = [];
            if (item.HasModel)
            {
                Result<IReadOnlyList<ShopeeItemModel>> modelResult = await shopeeGateway.GetModelListAsync(
                    connection.ShopId, connection.AccessToken, item.ItemId, cancellationToken);
                // A single item's model-list failure must not discard the rest of the page;
                // the item is returned without model detail and recovers on the next fetch.
                if (modelResult.IsSuccess)
                {
                    models = modelResult.Value
                        .Select(m => new ShopeeShopItemModelResponse(
                            m.ModelId,
                            m.ModelName,
                            m.ModelSku,
                            m.StockQuantity,
                            linksByUnit.GetValueOrDefault(UnitKey(item.ItemId, m.ModelId))))
                        .ToList();
                }
            }

            items.Add(new ShopeeShopItemResponse(
                item.ItemId,
                item.ItemName,
                item.ItemSku,
                item.ItemStatus,
                item.HasModel,
                item.StockQuantity,
                item.ImageUrl,
                linksByUnit.GetValueOrDefault(UnitKey(item.ItemId, 0)),
                models));
        }

        return Result<ShopeeShopItemsResponse>.Success(new ShopeeShopItemsResponse(
            items,
            page.HasNextPage,
            page.NextOffset,
            page.TotalCount));
    }

    private static string UnitKey(long itemId, long modelId) => $"{itemId}:{modelId}";
}
