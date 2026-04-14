using Wrapster.Application.Abstractions;
using Wrapster.Application.Abstractions.Messaging;
using Wrapster.Application.Waybills.Services;
using Wrapster.Domain.Abstractions;
using Wrapster.Domain.Common;
using Wrapster.Domain.Entities;
using Wrapster.Domain.Errors;
using Wrapster.Domain.Repositories;

namespace Wrapster.Application.Waybills.Commands.AddWaybillItem;

internal sealed class AddWaybillItemCommandHandler(
    IWaybillRepository waybillRepository,
    IProductRepository productRepository,
    StockReservationService stockReservationService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork) : ICommandHandler<AddWaybillItemCommand, Guid>
{
    public async Task<Result<Guid>> Handle(AddWaybillItemCommand request, CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        Waybill? waybill =
            await waybillRepository.GetByIdWithItemsAsync(request.WaybillId, tenantId, cancellationToken);
        if (waybill is null)
        {
            return Result<Guid>.Failure(WaybillErrors.NotFound);
        }

        Product? product =
            await productRepository.GetByBarcodeAsync(request.Barcode, tenantId, cancellationToken);
        if (product is null)
        {
            return Result<Guid>.Failure(ProductErrors.NotFound);
        }

        Result reserveResult =
            await stockReservationService.ReserveAsync(product.Id, request.Quantity, tenantId, cancellationToken);
        if (reserveResult.IsFailure)
        {
            return Result<Guid>.Failure(reserveResult.Error);
        }

        Result<WaybillItem> addResult = waybill.AddOrIncrementItem(product.Id, product.Barcode, request.Quantity);
        if (addResult.IsFailure)
        {
            return Result<Guid>.Failure(addResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return addResult.Value.Id;
    }
}
