using Microsoft.Extensions.Options;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Products.Common;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;
using TenantSettingsEntity = Wrapsfer.Domain.Entities.TenantSettings;

namespace Wrapsfer.Application.Products.Commands.CreateProduct;

internal sealed class CreateProductCommandHandler(
    IProductRepository productRepository,
    IProductComponentRepository productComponentRepository,
    ITenantSettingsRepository tenantSettingsRepository,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IOptions<ProductSettings> productSettings) : ICommandHandler<CreateProductCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        Product? existingByBarcode =
            await productRepository.GetByBarcodeAsync(request.Barcode, tenantId, cancellationToken);
        if (existingByBarcode is not null)
        {
            return Result<Guid>.Failure(ProductErrors.BarcodeAlreadyExists);
        }

        if (!string.IsNullOrWhiteSpace(request.SkuCode))
        {
            Product? existingBySku =
                await productRepository.GetBySkuCodeAsync(request.SkuCode, tenantId, cancellationToken);
            if (existingBySku is not null)
            {
                return Result<Guid>.Failure(ProductErrors.SkuAlreadyExists);
            }
        }

        if (request.Type == ProductType.Package)
        {
            if (!request.UnpackTargetProductId.HasValue)
            {
                return Result<Guid>.Failure(ProductErrors.MissingUnpackTarget);
            }

            Product? targetProduct = await productRepository.GetByIdAsync(
                request.UnpackTargetProductId.Value, tenantId, cancellationToken);

            if (targetProduct is null)
            {
                return Result<Guid>.Failure(ProductErrors.InvalidUnpackTarget);
            }

            if (targetProduct.Type != ProductType.Single)
            {
                return Result<Guid>.Failure(ProductErrors.InvalidUnpackTarget);
            }
        }

        if (request.Type == ProductType.Bundle && request.Components is { Count: > 0 })
        {
            HashSet<Guid> seen = [];
            foreach (BundleComponentInput component in request.Components)
            {
                if (!seen.Add(component.ChildProductId))
                {
                    return Result<Guid>.Failure(ProductErrors.DuplicateComponentChildId);
                }
            }

            List<Guid> childIds = request.Components.Select(c => c.ChildProductId).ToList();
            IReadOnlyList<Product> children =
                await productRepository.GetByIdsAsync(childIds, tenantId, cancellationToken);
            Dictionary<Guid, Product> childMap = children.ToDictionary(c => c.Id);

            foreach (BundleComponentInput component in request.Components)
            {
                if (!childMap.TryGetValue(component.ChildProductId, out Product? child))
                {
                    return Result<Guid>.Failure(ProductErrors.ComponentNotFound);
                }

                if (child.Type != ProductType.Single)
                {
                    return Result<Guid>.Failure(ProductErrors.InvalidComponentType);
                }
            }
        }

        Result<Product> productResult = Product.Create(
            tenantId,
            request.Barcode,
            request.Name,
            request.Type,
            request.Cost,
            request.StockQuantity,
            request.SkuCode,
            request.LowStockThreshold,
            request.UnpackTargetProductId,
            request.UnpackQuantityPerPackage);

        if (productResult.IsFailure)
        {
            return Result<Guid>.Failure(productResult.Error);
        }

        Product product = productResult.Value;

        TenantSettingsEntity? settings = await tenantSettingsRepository.GetByTenantIdAsync(tenantId, cancellationToken);
        int fallbackThreshold = settings?.DefaultLowStockThreshold ?? productSettings.Value.GlobalLowStockThreshold;
        product.CheckLowStock(fallbackThreshold);

        productRepository.Add(product);

        if (request.Type == ProductType.Bundle && request.Components is { Count: > 0 })
        {
            foreach (BundleComponentInput component in request.Components)
            {
                Result<ProductComponent> componentResult =
                    ProductComponent.Create(tenantId, product.Id, component.ChildProductId, component.Quantity);

                if (componentResult.IsFailure)
                {
                    return Result<Guid>.Failure(componentResult.Error);
                }

                productComponentRepository.Add(componentResult.Value);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return product.Id;
    }
}
