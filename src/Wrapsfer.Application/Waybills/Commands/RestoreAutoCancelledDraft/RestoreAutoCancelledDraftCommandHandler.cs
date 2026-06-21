using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Waybills.Services;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Waybills.Commands.RestoreAutoCancelledDraft;

internal sealed class RestoreAutoCancelledDraftCommandHandler(
    IWaybillRepository waybillRepository,
    StockReservationService stockReservationService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork) : ICommandHandler<RestoreAutoCancelledDraftCommand>
{
    public async Task<Result> Handle(RestoreAutoCancelledDraftCommand request, CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        Waybill? waybill =
            await waybillRepository.GetByIdWithItemsAsync(request.Id, tenantId, cancellationToken);
        if (waybill is null)
        {
            return Result.Failure(WaybillErrors.NotFound);
        }

        if (waybill.Status != WaybillStatus.Cancelled ||
            !string.Equals(waybill.CancellationReason, Waybill.AutoCancelledStaleDraftReason, StringComparison.Ordinal))
        {
            return Result.Failure(WaybillErrors.CannotRestoreNonAutoCancelledDraft);
        }

        Result reserveResult =
            await stockReservationService.ReserveItemsAsync(waybill.Items, tenantId, cancellationToken);
        if (reserveResult.IsFailure)
        {
            return reserveResult;
        }

        Result restoreResult = waybill.RestoreAutoCancelledDraft();
        if (restoreResult.IsFailure)
        {
            return restoreResult;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
