using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Application.Shopee.Services;

public sealed class ShopeeSellableUnitResolver(IShopeeGateway shopeeGateway)
{
    public async Task<Result<ShopeeSellableUnit>> ResolveAsync(
        ShopeeShopConnection connection,
        long itemId,
        long modelId,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<ShopeeItemDetail>> itemResult = await shopeeGateway.GetItemBaseInfoAsync(
            connection.ShopId, connection.AccessToken, [itemId], cancellationToken);
        if (itemResult.IsFailure)
        {
            return Result<ShopeeSellableUnit>.Failure(MapFetchError(itemResult.Error));
        }

        ShopeeItemDetail? item = itemResult.Value.FirstOrDefault(i => i.ItemId == itemId);
        if (item is null)
        {
            return Result<ShopeeSellableUnit>.Failure(ShopeeProductLinkErrors.ItemNotFoundInShop);
        }

        if (!item.HasModel)
        {
            if (modelId != 0)
            {
                return Result<ShopeeSellableUnit>.Failure(ShopeeProductLinkErrors.ModelMismatch);
            }

            return Result<ShopeeSellableUnit>.Success(new ShopeeSellableUnit(
                item.ItemId,
                0,
                item.ItemName,
                null,
                item.ItemSku,
                item.StockQuantity,
                false));
        }

        if (modelId == 0)
        {
            return Result<ShopeeSellableUnit>.Failure(ShopeeProductLinkErrors.ModelMismatch);
        }

        Result<IReadOnlyList<ShopeeItemModel>> modelResult = await shopeeGateway.GetModelListAsync(
            connection.ShopId, connection.AccessToken, itemId, cancellationToken);
        if (modelResult.IsFailure)
        {
            return Result<ShopeeSellableUnit>.Failure(MapFetchError(modelResult.Error));
        }

        ShopeeItemModel? model = modelResult.Value.FirstOrDefault(m => m.ModelId == modelId);
        if (model is null)
        {
            return Result<ShopeeSellableUnit>.Failure(ShopeeProductLinkErrors.ModelMismatch);
        }

        return Result<ShopeeSellableUnit>.Success(new ShopeeSellableUnit(
            item.ItemId,
            model.ModelId,
            item.ItemName,
            model.ModelName,
            model.ModelSku ?? item.ItemSku,
            model.StockQuantity,
            true));
    }

    private static Error MapFetchError(Error error) =>
        error.Code == ShopeeProductLinkErrors.AuthFailed.Code
            ? error
            : ShopeeProductLinkErrors.ItemFetchFailed;
}
