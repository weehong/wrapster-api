using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Waybills.Queries.CheckWaybillNumberAvailable;

internal sealed class CheckWaybillNumberAvailableQueryHandler(
    IWaybillRepository waybillRepository,
    ITenantContext tenantContext) : IQueryHandler<CheckWaybillNumberAvailableQuery, bool>
{
    public async Task<Result<bool>> Handle(CheckWaybillNumberAvailableQuery request,
        CancellationToken cancellationToken)
    {
        bool exists = await waybillRepository.ExistsByNumberAsync(
            request.WaybillNumber, tenantContext.TenantId, cancellationToken);
        return Result<bool>.Success(!exists);
    }
}
