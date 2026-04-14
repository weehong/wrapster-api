using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Waybills.Services;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Waybills.Commands.MarkWaybillHandedOff;

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
