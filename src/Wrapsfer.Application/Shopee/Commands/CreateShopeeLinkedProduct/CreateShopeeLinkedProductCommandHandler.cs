using Microsoft.Extensions.Options;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Products;
using Wrapsfer.Application.Shopee.Common;
using Wrapsfer.Application.Shopee.Responses;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;
using TenantSettingsEntity = Wrapsfer.Domain.Entities.TenantSettings;

namespace Wrapsfer.Application.Shopee.Commands.CreateShopeeLinkedProduct;

internal sealed class CreateShopeeLinkedProductCommandHandler(
    IProductRepository productRepository,
    IShopeeProductLinkRepository linkRepository,
    IShopeeShopConnectionRepository connectionRepository,
    ShopeeSellableUnitResolver sellableUnitResolver,
    ITenantSettingsRepository tenantSettingsRepository,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IOptions<ProductSettings> productSettings)
    : ICommandHandler<CreateShopeeLinkedProductCommand, ShopeeProductLinkResponse>
{
    public async Task<Result<ShopeeProductLinkResponse>> Handle(
        CreateShopeeLinkedProductCommand request,
        CancellationToken cancellationToken)
    {
        bool shopeeLinked = await linkRepository.ExistsAsync(
            request.TenantId, request.ShopeeItemId, request.ShopeeModelId, cancellationToken);
        if (shopeeLinked)
        {
            return Result<ShopeeProductLinkResponse>.Failure(ShopeeProductLinkErrors.AlreadyLinked);
        }

        Product? existingByBarcode = await productRepository.GetByBarcodeAsync(
            request.Barcode, request.TenantId, cancellationToken);
        if (existingByBarcode is not null)
        {
            return Result<ShopeeProductLinkResponse>.Failure(ProductErrors.BarcodeAlreadyExists);
        }

        if (!string.IsNullOrWhiteSpace(request.SkuCode))
        {
            Product? existingBySku = await productRepository.GetBySkuCodeAsync(
                request.SkuCode, request.TenantId, cancellationToken);
            if (existingBySku is not null)
            {
                return Result<ShopeeProductLinkResponse>.Failure(ProductErrors.SkuAlreadyExists);
            }
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

        Result<Product> productResult = Product.Create(
            request.TenantId,
            request.Barcode,
            request.Name,
            ProductType.Single,
            request.Cost,
            request.StockQuantity,
            request.SkuCode,
            request.LowStockThreshold);
        if (productResult.IsFailure)
        {
            return Result<ShopeeProductLinkResponse>.Failure(productResult.Error);
        }

        Product product = productResult.Value;
        TenantSettingsEntity? settings =
            await tenantSettingsRepository.GetByTenantIdAsync(request.TenantId, cancellationToken);
        int fallbackThreshold =
            settings?.DefaultLowStockThreshold ?? productSettings.Value.GlobalLowStockThreshold;
        product.CheckLowStock(fallbackThreshold);

        ShopeeSellableUnit unit = unitResult.Value;
        Result<ShopeeProductLink> linkResult = ShopeeProductLink.Create(
            request.TenantId,
            product.Id,
            unit.ItemId,
            unit.ModelId,
            unit.ItemName,
            unit.ModelName,
            unit.Sku,
            tenantContext.Username);
        if (linkResult.IsFailure)
        {
            return Result<ShopeeProductLinkResponse>.Failure(linkResult.Error);
        }

        ShopeeProductLink link = linkResult.Value;
        productRepository.Add(product);
        linkRepository.Add(link);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<ShopeeProductLinkResponse>.Success(
            ShopeeProductLinkResponseMapper.Map(link, product));
    }
}
