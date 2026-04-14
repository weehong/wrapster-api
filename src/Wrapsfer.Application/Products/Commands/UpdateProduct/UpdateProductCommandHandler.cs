using Microsoft.Extensions.Options;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
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

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
