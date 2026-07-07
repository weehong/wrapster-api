using Microsoft.Extensions.Options;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Products;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;
using TenantSettingsEntity = Wrapsfer.Domain.Entities.TenantSettings;

namespace Wrapsfer.Application.PurchaseOrders.Commands.ReceivePurchaseOrder;

internal sealed class ReceivePurchaseOrderCommandHandler(
    IPurchaseOrderRepository purchaseOrderRepository,
    IProductRepository productRepository,
    ITenantSettingsRepository tenantSettingsRepository,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IOptions<ProductSettings> productSettings) : ICommandHandler<ReceivePurchaseOrderCommand>
{
    public async Task<Result> Handle(ReceivePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        PurchaseOrder? purchaseOrder =
            await purchaseOrderRepository.GetByIdAsync(request.Id, tenantId, cancellationToken);
        if (purchaseOrder is null)
        {
            return Result.Failure(PurchaseOrderErrors.NotFound);
        }

        Result receiveResult = purchaseOrder.Receive();
        if (receiveResult.IsFailure)
        {
            return receiveResult;
        }

        Product? product =
            await productRepository.GetByIdAsync(purchaseOrder.ProductId, tenantId, cancellationToken);
        if (product is null)
        {
            return Result.Failure(PurchaseOrderErrors.ProductNotFound);
        }

        TenantSettingsEntity? settings =
            await tenantSettingsRepository.GetByTenantIdAsync(tenantId, cancellationToken);
        int fallbackThreshold =
            settings?.DefaultLowStockThreshold ?? productSettings.Value.GlobalLowStockThreshold;

        Result restoreResult = product.RestoreStock(purchaseOrder.Quantity, fallbackThreshold);
        if (restoreResult.IsFailure)
        {
            return restoreResult;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
