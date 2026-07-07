using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Common;
using Wrapsfer.Application.PurchaseOrders.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.PurchaseOrders.Queries.ListPurchaseOrders;

internal sealed class ListPurchaseOrdersQueryHandler(
    IPurchaseOrderRepository purchaseOrderRepository,
    IPartnerTenantRepository partnerTenantRepository,
    ITenantContext tenantContext) : IQueryHandler<ListPurchaseOrdersQuery, PagedResult<PurchaseOrderResponse>>
{
    public async Task<Result<PagedResult<PurchaseOrderResponse>>> Handle(ListPurchaseOrdersQuery request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<PurchaseOrder> items;
        int totalCount;

        if (request.IncludeAllPartnerTenants)
        {
            IReadOnlyList<PartnerTenant> partnerTenants =
                await partnerTenantRepository.ListAsync(cancellationToken: cancellationToken);
            IReadOnlyList<string> tenantIds = partnerTenants.Select(p => p.TenantId).ToList();

            (items, totalCount) = await purchaseOrderRepository.ListByTenantIdsAsync(
                tenantIds,
                request.Status,
                request.Search,
                request.Page,
                request.PageSize,
                cancellationToken);
        }
        else
        {
            (items, totalCount) = await purchaseOrderRepository.ListAsync(
                tenantContext.TenantId,
                request.Status,
                request.Search,
                request.Page,
                request.PageSize,
                cancellationToken);
        }

        List<PurchaseOrderResponse> responses = items
            .Select(PurchaseOrderResponseMapper.ToResponse)
            .ToList();

        return new PagedResult<PurchaseOrderResponse>(responses, totalCount, request.Page, request.PageSize);
    }
}
