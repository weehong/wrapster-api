using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Waybills.Commands.MarkWaybillPacked;

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
