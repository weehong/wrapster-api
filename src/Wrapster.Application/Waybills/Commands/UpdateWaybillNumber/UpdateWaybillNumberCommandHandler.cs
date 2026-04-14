using Wrapster.Application.Abstractions;
using Wrapster.Application.Abstractions.Messaging;
using Wrapster.Domain.Abstractions;
using Wrapster.Domain.Common;
using Wrapster.Domain.Entities;
using Wrapster.Domain.Errors;
using Wrapster.Domain.Repositories;

namespace Wrapster.Application.Waybills.Commands.UpdateWaybillNumber;

internal sealed class UpdateWaybillNumberCommandHandler(
    IWaybillRepository waybillRepository,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork) : ICommandHandler<UpdateWaybillNumberCommand>
{
    public async Task<Result> Handle(UpdateWaybillNumberCommand request, CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        Waybill? waybill = await waybillRepository.GetByIdAsync(request.Id, tenantId, cancellationToken);
        if (waybill is null)
        {
            return Result.Failure(WaybillErrors.NotFound);
        }

        bool duplicate = await waybillRepository.ExistsByNumberAsync(
            request.WaybillNumber, request.Id, tenantId, cancellationToken);
        if (duplicate)
        {
            return Result.Failure(WaybillErrors.DuplicateWaybillNumber);
        }

        Result updateResult = waybill.UpdateWaybillNumber(request.WaybillNumber);
        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
