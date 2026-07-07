using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Waybills.Commands.UpdateWaybillNumber;

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

        bool duplicate = await waybillRepository.ExistsByNumberInAnyTenantAsync(
            request.WaybillNumber.Trim(), request.Id, cancellationToken);
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
