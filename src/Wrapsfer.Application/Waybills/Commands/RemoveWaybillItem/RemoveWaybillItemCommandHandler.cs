using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Waybills.Services;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Waybills.Commands.RemoveWaybillItem;

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
