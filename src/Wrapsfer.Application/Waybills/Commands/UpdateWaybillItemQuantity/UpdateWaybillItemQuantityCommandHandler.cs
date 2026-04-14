using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Waybills.Services;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Waybills.Commands.UpdateWaybillItemQuantity;

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
