using Wrapster.Application.Abstractions;
using Wrapster.Application.Abstractions.Messaging;
using Wrapster.Domain.Abstractions;
using Wrapster.Domain.Common;
using Wrapster.Domain.Entities;
using Wrapster.Domain.Errors;
using Wrapster.Domain.Repositories;

namespace Wrapster.Application.Waybills.Commands.CreateWaybill;

internal sealed class CreateWaybillCommandHandler(
    IWaybillRepository waybillRepository,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork) : ICommandHandler<CreateWaybillCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateWaybillCommand request, CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        bool exists =
            await waybillRepository.ExistsByNumberAsync(request.WaybillNumber, tenantId, cancellationToken);
        if (exists)
        {
            return Result<Guid>.Failure(WaybillErrors.DuplicateWaybillNumber);
        }

        Result<Waybill> waybillResult = Waybill.Create(tenantId, request.PackagingDate, request.WaybillNumber);
        if (waybillResult.IsFailure)
        {
            return Result<Guid>.Failure(waybillResult.Error);
        }

        waybillRepository.Add(waybillResult.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return waybillResult.Value.Id;
    }
}
