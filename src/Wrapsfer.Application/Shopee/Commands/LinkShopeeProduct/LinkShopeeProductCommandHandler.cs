using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Shopee.Common;
using Wrapsfer.Application.Shopee.Responses;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Shopee.Commands.LinkShopeeProduct;

internal sealed class LinkShopeeProductCommandHandler(
    IProductRepository productRepository,
    IShopeeProductLinkRepository linkRepository,
    IShopeeShopConnectionRepository connectionRepository,
    ShopeeSellableUnitResolver sellableUnitResolver,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : ICommandHandler<LinkShopeeProductCommand, ShopeeProductLinkResponse>
{
    public async Task<Result<ShopeeProductLinkResponse>> Handle(
        LinkShopeeProductCommand request,
        CancellationToken cancellationToken)
    {
        Product? product = await productRepository.GetByIdAsync(
            request.ProductId, request.TenantId, cancellationToken);
        if (product is null)
        {
            return Result<ShopeeProductLinkResponse>.Failure(ShopeeProductLinkErrors.ProductNotFound);
        }

        if (!product.IsActive)
        {
            return Result<ShopeeProductLinkResponse>.Failure(ShopeeProductLinkErrors.ProductInactive);
        }

        if (product.Type == ProductType.Bundle)
        {
            return Result<ShopeeProductLinkResponse>.Failure(ShopeeProductLinkErrors.BundleProductNotAllowed);
        }

        bool productLinked = await linkRepository.ExistsForProductAsync(
            request.TenantId, request.ProductId, cancellationToken);
        if (productLinked)
        {
            return Result<ShopeeProductLinkResponse>.Failure(ShopeeProductLinkErrors.ProductAlreadyLinked);
        }

        bool shopeeLinked = await linkRepository.ExistsAsync(
            request.TenantId, request.ShopeeItemId, request.ShopeeModelId, cancellationToken);
        if (shopeeLinked)
        {
            return Result<ShopeeProductLinkResponse>.Failure(ShopeeProductLinkErrors.AlreadyLinked);
        }

        ShopeeShopConnection? connection =
            await connectionRepository.GetByTenantIdAsync(request.TenantId, cancellationToken);
        if (connection is null)
        {
            return Result<ShopeeProductLinkResponse>.Failure(ShopeeProductLinkErrors.ConnectionNotFound);
        }

        Result<ShopeeSellableUnit> unitResult = await sellableUnitResolver.ResolveAsync(
            connection, request.ShopeeItemId, request.ShopeeModelId, cancellationToken);
        if (unitResult.IsFailure)
        {
            return Result<ShopeeProductLinkResponse>.Failure(unitResult.Error);
        }

        ShopeeSellableUnit unit = unitResult.Value;
        Result<ShopeeProductLink> createResult = ShopeeProductLink.Create(
            request.TenantId,
            product.Id,
            unit.ItemId,
            unit.ModelId,
            unit.ItemName,
            unit.ModelName,
            unit.Sku,
            tenantContext.Username);
        if (createResult.IsFailure)
        {
            return Result<ShopeeProductLinkResponse>.Failure(createResult.Error);
        }

        ShopeeProductLink link = createResult.Value;
        linkRepository.Add(link);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<ShopeeProductLinkResponse>.Success(
            ShopeeProductLinkResponseMapper.Map(link, product));
    }
}
