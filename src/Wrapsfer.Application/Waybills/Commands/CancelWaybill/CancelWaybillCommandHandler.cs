using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Waybills.Services;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Waybills.Commands.CancelWaybill;

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
