using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Shopee.Common;
using Wrapsfer.Application.Shopee.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Shopee.Queries.GetShopeeOrderDetail;

internal sealed class GetShopeeOrderDetailQueryHandler(
    IShopeeOrderRepository orderRepository) : IQueryHandler<GetShopeeOrderDetailQuery, ShopeeOrderResponse>
{
    public async Task<Result<ShopeeOrderResponse>> Handle(
        GetShopeeOrderDetailQuery request, CancellationToken cancellationToken)
    {
        ShopeeOrder? order = await orderRepository.GetByIdAsync(
            request.OrderId, request.TenantId, cancellationToken);
        if (order is null)
        {
            return Result<ShopeeOrderResponse>.Failure(ShopeeOrderErrors.NotFound);
        }

        return Result<ShopeeOrderResponse>.Success(ShopeeOrderResponseMapper.Map(order));
    }
}
