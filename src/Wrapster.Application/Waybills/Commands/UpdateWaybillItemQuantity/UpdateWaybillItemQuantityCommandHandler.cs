using Wrapster.Application.Abstractions;
using Wrapster.Application.Abstractions.Messaging;
using Wrapster.Application.Waybills.Services;
using Wrapster.Domain.Abstractions;
using Wrapster.Domain.Common;
using Wrapster.Domain.Entities;
using Wrapster.Domain.Errors;
using Wrapster.Domain.Repositories;

namespace Wrapster.Application.Waybills.Commands.UpdateWaybillItemQuantity;

internal sealed class UpdateWaybillItemQuantityCommandHandler(
    IWaybillRepository waybillRepository,
    StockReservationService stockReservationService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork) : ICommandHandler<UpdateWaybillItemQuantityCommand>
{
    public async Task<Result> Handle(UpdateWaybillItemQuantityCommand request, CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        Waybill? waybill =
            await waybillRepository.GetByIdWithItemsAsync(request.WaybillId, tenantId, cancellationToken);
        if (waybill is null)
        {
            return Result.Failure(WaybillErrors.NotFound);
        }

        Result<QuantityChange> updateResult = waybill.UpdateItemQuantity(request.ItemId, request.Quantity);
        if (updateResult.IsFailure)
        {
            return Result.Failure(updateResult.Error);
        }

        QuantityChange change = updateResult.Value;

        if (change.Delta > 0)
        {
            Result reserveResult =
                await stockReservationService.ReserveAsync(change.ProductId, change.Delta, tenantId,
                    cancellationToken);
            if (reserveResult.IsFailure)
            {
                return reserveResult;
            }
        }
        else if (change.Delta < 0)
        {
            Result releaseResult =
                await stockReservationService.ReleaseAsync(change.ProductId, -change.Delta, tenantId,
                    cancellationToken);
            if (releaseResult.IsFailure)
            {
                return releaseResult;
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
