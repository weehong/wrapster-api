using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Waybills.Commands.CreateWaybill;

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
