using Wrapster.Application.Abstractions;
using Wrapster.Application.Abstractions.Messaging;
using Wrapster.Application.Waybills.Services;
using Wrapster.Domain.Abstractions;
using Wrapster.Domain.Common;
using Wrapster.Domain.Entities;
using Wrapster.Domain.Errors;
using Wrapster.Domain.Repositories;

namespace Wrapster.Application.Waybills.Commands.RemoveWaybillItem;

internal sealed class RemoveWaybillItemCommandHandler(
    IWaybillRepository waybillRepository,
    StockReservationService stockReservationService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork) : ICommandHandler<RemoveWaybillItemCommand>
{
    public async Task<Result> Handle(RemoveWaybillItemCommand request, CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        Waybill? waybill =
            await waybillRepository.GetByIdWithItemsAsync(request.WaybillId, tenantId, cancellationToken);
        if (waybill is null)
        {
            return Result.Failure(WaybillErrors.NotFound);
        }

        Result<WaybillItemRemoval> removeResult = waybill.RemoveItem(request.ItemId);
        if (removeResult.IsFailure)
        {
            return Result.Failure(removeResult.Error);
        }

        WaybillItemRemoval removal = removeResult.Value;
        Result releaseResult = await stockReservationService.ReleaseAsync(
            removal.ProductId, removal.Quantity, tenantId, cancellationToken);
        if (releaseResult.IsFailure)
        {
            return releaseResult;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
