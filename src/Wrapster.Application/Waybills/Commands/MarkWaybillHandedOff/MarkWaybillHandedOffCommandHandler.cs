using Wrapster.Application.Abstractions;
using Wrapster.Application.Abstractions.Messaging;
using Wrapster.Application.Waybills.Services;
using Wrapster.Domain.Abstractions;
using Wrapster.Domain.Common;
using Wrapster.Domain.Entities;
using Wrapster.Domain.Errors;
using Wrapster.Domain.Repositories;

namespace Wrapster.Application.Waybills.Commands.MarkWaybillHandedOff;

internal sealed class MarkWaybillHandedOffCommandHandler(
    IWaybillRepository waybillRepository,
    StockReservationService stockReservationService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork) : ICommandHandler<MarkWaybillHandedOffCommand>
{
    public async Task<Result> Handle(MarkWaybillHandedOffCommand request, CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        Waybill? waybill =
            await waybillRepository.GetByIdWithItemsAsync(request.Id, tenantId, cancellationToken);
        if (waybill is null)
        {
            return Result.Failure(WaybillErrors.NotFound);
        }

        Result markResult = waybill.MarkHandedOff(tenantContext.UserId);
        if (markResult.IsFailure)
        {
            return markResult;
        }

        Result consumeResult =
            await stockReservationService.ConsumeItemsAsync(waybill.Items, tenantId, cancellationToken);
        if (consumeResult.IsFailure)
        {
            return consumeResult;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
