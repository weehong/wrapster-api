using Wrapster.Application.Abstractions;
using Wrapster.Application.Abstractions.Messaging;
using Wrapster.Application.Waybills.Services;
using Wrapster.Domain.Abstractions;
using Wrapster.Domain.Common;
using Wrapster.Domain.Entities;
using Wrapster.Domain.Enums;
using Wrapster.Domain.Errors;
using Wrapster.Domain.Repositories;

namespace Wrapster.Application.Waybills.Commands.CancelWaybill;

internal sealed class CancelWaybillCommandHandler(
    IWaybillRepository waybillRepository,
    StockReservationService stockReservationService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork) : ICommandHandler<CancelWaybillCommand>
{
    public async Task<Result> Handle(CancelWaybillCommand request, CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        Waybill? waybill =
            await waybillRepository.GetByIdWithItemsAsync(request.Id, tenantId, cancellationToken);
        if (waybill is null)
        {
            return Result.Failure(WaybillErrors.NotFound);
        }

        WaybillStatus previousStatus = waybill.Status;
        Result cancelResult = waybill.Cancel(request.Reason);
        if (cancelResult.IsFailure)
        {
            return cancelResult;
        }

        if (previousStatus == WaybillStatus.Draft || previousStatus == WaybillStatus.Packed)
        {
            Result releaseResult =
                await stockReservationService.ReleaseItemsAsync(waybill.Items, tenantId, cancellationToken);
            if (releaseResult.IsFailure)
            {
                return releaseResult;
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
