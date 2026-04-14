using Wrapster.Application.Abstractions;
using Wrapster.Application.Abstractions.Messaging;
using Wrapster.Application.Common;
using Wrapster.Application.Waybills.Common;
using Wrapster.Application.Waybills.Responses;
using Wrapster.Domain.Common;
using Wrapster.Domain.Entities;
using Wrapster.Domain.Repositories;

namespace Wrapster.Application.Waybills.Queries.ListWaybills;

internal sealed class ListWaybillsQueryHandler(
    IWaybillRepository waybillRepository,
    IProductRepository productRepository,
    ITenantContext tenantContext) : IQueryHandler<ListWaybillsQuery, PagedResult<WaybillResponse>>
{
    public async Task<Result<PagedResult<WaybillResponse>>> Handle(ListWaybillsQuery request,
        CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        (IReadOnlyList<Waybill> items, int totalCount) = await waybillRepository.ListAsync(
            tenantId,
            request.From,
            request.To,
            request.Status,
            request.Search,
            request.Page,
            request.PageSize,
            cancellationToken);

        HashSet<Guid> productIds = items
            .SelectMany(w => w.Items)
            .Select(i => i.ProductId)
            .ToHashSet();

        IReadOnlyDictionary<Guid, string> namesById = productIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await productRepository.GetByIdsAsync(productIds, tenantId, cancellationToken))
            .ToDictionary(p => p.Id, p => p.Name);

        List<WaybillResponse> responses = items
            .Select(w => WaybillResponseMapper.ToResponse(w, namesById))
            .ToList();

        return new PagedResult<WaybillResponse>(responses, totalCount, request.Page, request.PageSize);
    }
}
