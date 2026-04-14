using Wrapster.Application.Abstractions;
using Wrapster.Application.Abstractions.Messaging;
using Wrapster.Application.Waybills.Common;
using Wrapster.Application.Waybills.Responses;
using Wrapster.Domain.Common;
using Wrapster.Domain.Entities;
using Wrapster.Domain.Errors;
using Wrapster.Domain.Repositories;

namespace Wrapster.Application.Waybills.Queries.GetWaybillById;

internal sealed class GetWaybillByIdQueryHandler(
    IWaybillRepository waybillRepository,
    IProductRepository productRepository,
    ITenantContext tenantContext) : IQueryHandler<GetWaybillByIdQuery, WaybillResponse>
{
    public async Task<Result<WaybillResponse>> Handle(GetWaybillByIdQuery request,
        CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        Waybill? waybill =
            await waybillRepository.GetByIdWithItemsAsync(request.Id, tenantId, cancellationToken);
        if (waybill is null)
        {
            return Result<WaybillResponse>.Failure(WaybillErrors.NotFound);
        }

        HashSet<Guid> productIds = waybill.Items.Select(i => i.ProductId).ToHashSet();
        IReadOnlyDictionary<Guid, string> namesById = productIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await productRepository.GetByIdsAsync(productIds, tenantId, cancellationToken))
            .ToDictionary(p => p.Id, p => p.Name);

        return WaybillResponseMapper.ToResponse(waybill, namesById);
    }
}
