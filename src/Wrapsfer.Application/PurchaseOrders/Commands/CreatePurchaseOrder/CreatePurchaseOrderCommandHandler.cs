using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.PurchaseOrders.Commands.CreatePurchaseOrder;

internal sealed class CreatePurchaseOrderCommandHandler(
    IPurchaseOrderRepository purchaseOrderRepository,
    IProductRepository productRepository,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork) : ICommandHandler<CreatePurchaseOrderCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreatePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        bool exists =
            await purchaseOrderRepository.ExistsByNumberAsync(request.PoNumber, tenantId, cancellationToken);
        if (exists)
        {
            return Result<Guid>.Failure(PurchaseOrderErrors.DuplicatePoNumber);
        }

        Product? product = await productRepository.GetByIdAsync(request.ProductId, tenantId, cancellationToken);
        if (product is null)
        {
            return Result<Guid>.Failure(PurchaseOrderErrors.ProductNotFound);
        }

        if (!product.IsActive)
        {
            return Result<Guid>.Failure(PurchaseOrderErrors.ProductInactive);
        }

        if (product.Type == ProductType.Bundle)
        {
            return Result<Guid>.Failure(PurchaseOrderErrors.ProductNotStockable);
        }

        Result<PurchaseOrder> createResult = PurchaseOrder.Create(
            tenantId,
            request.PoNumber,
            product.Id,
            product.Barcode,
            product.Name,
            request.Quantity);
        if (createResult.IsFailure)
        {
            return Result<Guid>.Failure(createResult.Error);
        }

        purchaseOrderRepository.Add(createResult.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return createResult.Value.Id;
    }
}
