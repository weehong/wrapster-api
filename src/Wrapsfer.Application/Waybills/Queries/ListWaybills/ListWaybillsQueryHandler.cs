using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Common;
using Wrapsfer.Application.Waybills.Common;
using Wrapsfer.Application.Waybills.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Waybills.Queries.ListWaybills;

internal sealed class ListWaybillsQueryHandler(
    IWaybillRepository waybillRepository,
    IProductRepository productRepository,
    IPartnerTenantRepository partnerTenantRepository,
    ITenantContext tenantContext) : IQueryHandler<ListWaybillsQuery, PagedResult<WaybillResponse>>
{
    public async Task<Result<PagedResult<WaybillResponse>>> Handle(ListWaybillsQuery request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Waybill> items;
        int totalCount;
        IReadOnlyDictionary<Guid, string> namesById;

        if (request.IncludeAllPartnerTenants)
        {
            IReadOnlyList<PartnerTenant> partnerTenants = await partnerTenantRepository.ListAsync(cancellationToken);
            IReadOnlyList<string> tenantIds = partnerTenants.Select(p => p.TenantId).ToList();

            (items, totalCount) = await waybillRepository.ListByTenantIdsAsync(
                tenantIds,
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

            namesById = productIds.Count == 0
                ? new Dictionary<Guid, string>()
                : (await productRepository.GetByIdsByTenantIdsAsync(productIds, tenantIds, cancellationToken))
                .ToDictionary(p => p.Id, p => p.Name);
        }
        else
        {
            string tenantId = tenantContext.TenantId;

            (items, totalCount) = await waybillRepository.ListAsync(
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

            namesById = productIds.Count == 0
                ? new Dictionary<Guid, string>()
                : (await productRepository.GetByIdsAsync(productIds, tenantId, cancellationToken))
                .ToDictionary(p => p.Id, p => p.Name);
        }

        List<WaybillResponse> responses = items
            .Select(w => WaybillResponseMapper.ToResponse(w, namesById))
            .ToList();

        return new PagedResult<WaybillResponse>(responses, totalCount, request.Page, request.PageSize);
    }
}
