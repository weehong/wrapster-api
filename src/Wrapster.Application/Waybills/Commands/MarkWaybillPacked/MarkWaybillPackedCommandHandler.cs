using Wrapster.Application.Abstractions;
using Wrapster.Application.Abstractions.Messaging;
using Wrapster.Domain.Abstractions;
using Wrapster.Domain.Common;
using Wrapster.Domain.Entities;
using Wrapster.Domain.Errors;
using Wrapster.Domain.Repositories;

namespace Wrapster.Application.Waybills.Commands.MarkWaybillPacked;

internal sealed class MarkWaybillPackedCommandHandler(
    IWaybillRepository waybillRepository,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork) : ICommandHandler<MarkWaybillPackedCommand>
{
    public async Task<Result> Handle(MarkWaybillPackedCommand request, CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        Waybill? waybill =
            await waybillRepository.GetByIdWithItemsAsync(request.Id, tenantId, cancellationToken);
        if (waybill is null)
        {
            return Result.Failure(WaybillErrors.NotFound);
        }

        Result markResult = waybill.MarkPacked();
        if (markResult.IsFailure)
        {
            return markResult;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
