using Microsoft.Extensions.Options;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;
using TenantSettingsEntity = Wrapsfer.Domain.Entities.TenantSettings;

namespace Wrapsfer.Application.Products.Commands.UnpackPackage;

internal sealed class UnpackPackageCommandHandler(
    IProductRepository productRepository,
    ITenantSettingsRepository tenantSettingsRepository,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IOptions<ProductSettings> productSettings) : ICommandHandler<UnpackPackageCommand>
{
    public async Task<Result> Handle(UnpackPackageCommand request, CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        Product? package = await productRepository.GetByIdAsync(request.ProductId, tenantId, cancellationToken);
        if (package is null)
        {
            return Result.Failure(ProductErrors.NotFound);
        }

        Result<int> unpackResult = package.Unpack(request.Quantity);
        if (unpackResult.IsFailure)
        {
            return Result.Failure(unpackResult.Error);
        }

        Product? targetProduct = await productRepository.GetByIdAsync(
            package.UnpackTargetProductId!.Value, tenantId, cancellationToken);
        if (targetProduct is null)
        {
            return Result.Failure(ProductErrors.InvalidUnpackTarget);
        }

        TenantSettingsEntity? settings = await tenantSettingsRepository.GetByTenantIdAsync(tenantId, cancellationToken);
        int fallbackThreshold = settings?.DefaultLowStockThreshold ?? productSettings.Value.GlobalLowStockThreshold;

        Result restoreResult = targetProduct.RestoreStock(unpackResult.Value, fallbackThreshold);
        if (restoreResult.IsFailure)
        {
            return restoreResult;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
