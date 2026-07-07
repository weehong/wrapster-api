using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Waybills.Queries.CheckWaybillNumberAvailable;

internal sealed class CheckWaybillNumberAvailableQueryHandler(
    IWaybillRepository waybillRepository) : IQueryHandler<CheckWaybillNumberAvailableQuery, bool>
{
    public async Task<Result<bool>> Handle(CheckWaybillNumberAvailableQuery request,
        CancellationToken cancellationToken)
    {
        bool exists = await waybillRepository.ExistsByNumberInAnyTenantAsync(
            request.WaybillNumber.Trim(), cancellationToken);
        return Result<bool>.Success(!exists);
    }
}
