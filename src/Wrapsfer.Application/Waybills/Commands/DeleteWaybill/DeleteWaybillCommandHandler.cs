using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Waybills.Services;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Waybills.Commands.DeleteWaybill;

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
