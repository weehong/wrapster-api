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

namespace Wrapsfer.Application.Products.Commands.UpdateProduct;

internal sealed class UpdateProductCommandHandler(
    IProductRepository productRepository,
    IProductComponentRepository productComponentRepository,
    ITenantSettingsRepository tenantSettingsRepository,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IOptions<ProductSettings> productSettings) : ICommandHandler<UpdateProductCommand>
{
    public async Task<Result> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        Product? product = await productRepository.GetByIdAsync(request.Id, tenantId, cancellationToken);
        if (product is null)
        {
            return Result.Failure(ProductErrors.NotFound);
        }

        if (!request.ClearSkuCode && !string.IsNullOrWhiteSpace(request.SkuCode) && request.SkuCode != product.SkuCode)
        {
            Product? existingBySku =
                await productRepository.GetBySkuCodeAsync(request.SkuCode, tenantId, cancellationToken);
            if (existingBySku is not null)
            {
                return Result.Failure(ProductErrors.SkuAlreadyExists);
            }
        }

        if (request.Components is not null && product.Type != ProductType.Bundle)
        {
            return Result.Failure(ProductErrors.ComponentsOnNonBundle);
        }

        TenantSettingsEntity? settings = await tenantSettingsRepository.GetByTenantIdAsync(tenantId, cancellationToken);
        int fallbackThreshold = settings?.DefaultLowStockThreshold ?? productSettings.Value.GlobalLowStockThreshold;

        product.Update(request.Name, request.SkuCode, request.ClearSkuCode, request.Cost, request.LowStockThreshold,
            request.ClearLowStockThreshold, fallbackThreshold);

        if (request.UnpackTargetProductId.HasValue && request.UnpackQuantityPerPackage.HasValue)
        {
            Product? targetProduct = await productRepository.GetByIdAsync(
                request.UnpackTargetProductId.Value, tenantId, cancellationToken);

            if (targetProduct is null || targetProduct.Type != ProductType.Single)
            {
                return Result.Failure(ProductErrors.InvalidUnpackTarget);
            }

            Result unpackConfigResult = product.UpdateUnpackConfig(
                request.UnpackTargetProductId.Value,
                request.UnpackQuantityPerPackage.Value);

            if (unpackConfigResult.IsFailure)
            {
                return unpackConfigResult;
            }
        }

        if (request.Components is not null)
        {
            Result componentsResult = await ReplaceBundleComponentsAsync(
                product, request.Components, tenantId, cancellationToken);

            if (componentsResult.IsFailure)
            {
                return componentsResult;
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<Result> ReplaceBundleComponentsAsync(
        Product bundle,
        IReadOnlyList<BundleComponentInput> components,
        string tenantId,
        CancellationToken cancellationToken)
    {
        HashSet<Guid> seen = [];
        foreach (BundleComponentInput component in components)
        {
            if (!seen.Add(component.ChildProductId))
            {
                return Result.Failure(ProductErrors.DuplicateComponentChildId);
            }
        }

        List<Guid> childIds = components.Select(c => c.ChildProductId).ToList();
        IReadOnlyList<Product> children =
            await productRepository.GetByIdsAsync(childIds, tenantId, cancellationToken);
        Dictionary<Guid, Product> childMap = children.ToDictionary(c => c.Id);

        foreach (BundleComponentInput component in components)
        {
            if (!childMap.TryGetValue(component.ChildProductId, out Product? child))
            {
                return Result.Failure(ProductErrors.ComponentNotFound);
            }

            if (child.Type != ProductType.Single)
            {
                return Result.Failure(ProductErrors.InvalidComponentType);
            }

            if (child.Id == bundle.Id)
            {
                return Result.Failure(ProductErrors.SelfReferencingComponent);
            }
        }

        await productComponentRepository.RemoveAllByParentIdAsync(bundle.Id, tenantId, cancellationToken);

        foreach (BundleComponentInput component in components)
        {
            Result<ProductComponent> componentResult =
                ProductComponent.Create(tenantId, bundle.Id, component.ChildProductId, component.Quantity);

            if (componentResult.IsFailure)
            {
                return Result.Failure(componentResult.Error);
            }

            productComponentRepository.Add(componentResult.Value);
        }

        return Result.Success();
    }
}
