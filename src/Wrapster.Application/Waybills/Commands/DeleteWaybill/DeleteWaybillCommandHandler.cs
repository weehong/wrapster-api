using Wrapster.Application.Abstractions;
using Wrapster.Application.Abstractions.Messaging;
using Wrapster.Application.Waybills.Services;
using Wrapster.Domain.Abstractions;
using Wrapster.Domain.Common;
using Wrapster.Domain.Entities;
using Wrapster.Domain.Enums;
using Wrapster.Domain.Errors;
using Wrapster.Domain.Repositories;

namespace Wrapster.Application.Waybills.Commands.DeleteWaybill;

internal sealed class DeleteWaybillCommandHandler(
    IWaybillRepository waybillRepository,
    StockReservationService stockReservationService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork) : ICommandHandler<DeleteWaybillCommand>
{
    public async Task<Result> Handle(DeleteWaybillCommand request, CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        Waybill? waybill =
            await waybillRepository.GetByIdWithItemsAsync(request.Id, tenantId, cancellationToken);
        if (waybill is null)
        {
            return Result.Failure(WaybillErrors.NotFound);
        }

        if (waybill.Status != WaybillStatus.Draft)
        {
            return Result.Failure(WaybillErrors.CannotDeleteNonDraft);
        }

        if (waybill.Items.Count > 0)
        {
            Result releaseResult =
                await stockReservationService.ReleaseItemsAsync(waybill.Items, tenantId, cancellationToken);
            if (releaseResult.IsFailure)
            {
                return releaseResult;
            }
        }

        waybillRepository.Remove(waybill);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
