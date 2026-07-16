using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Shopee.Common;
using Wrapsfer.Application.Shopee.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Shopee.Queries.GetShopeeOrders;

internal sealed class GetShopeeOrdersQueryHandler(
    IShopeeOrderRepository orderRepository) : IQueryHandler<GetShopeeOrdersQuery, ShopeeOrdersResponse>
{
    public async Task<Result<ShopeeOrdersResponse>> Handle(
        GetShopeeOrdersQuery request, CancellationToken cancellationToken)
    {
        (IReadOnlyList<ShopeeOrder> items, int totalCount) = await orderRepository.ListAsync(
            request.TenantId,
            request.Status,
            request.Search,
            request.Page,
            request.PageSize,
            cancellationToken);

        ShopeeOrdersResponse response = new(
            items.Select(ShopeeOrderResponseMapper.Map).ToList(),
            totalCount,
            request.Page,
            request.PageSize);

        return Result<ShopeeOrdersResponse>.Success(response);
    }
}
