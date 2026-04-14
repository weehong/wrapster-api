using Wrapster.Application.Abstractions;
using Wrapster.Application.Abstractions.Messaging;
using Wrapster.Domain.Common;
using Wrapster.Domain.Repositories;

namespace Wrapster.Application.Waybills.Queries.CheckWaybillNumberAvailable;

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
