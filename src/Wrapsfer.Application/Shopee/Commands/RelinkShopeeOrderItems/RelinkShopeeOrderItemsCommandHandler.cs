using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Shopee.Common;
using Wrapsfer.Application.Shopee.Responses;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Shopee.Commands.RelinkShopeeOrderItems;

internal sealed class RelinkShopeeOrderItemsCommandHandler(
    IShopeeOrderRepository orderRepository,
    IShopeeShopConnectionRepository connectionRepository,
    ShopeeOrderIngestionService ingestionService) : ICommandHandler<RelinkShopeeOrderItemsCommand, ShopeeOrderResponse>
{
    public async Task<Result<ShopeeOrderResponse>> Handle(
        RelinkShopeeOrderItemsCommand request, CancellationToken cancellationToken)
    {
        ShopeeOrder? order = await orderRepository.GetByIdAsync(
            request.OrderId, request.TenantId, cancellationToken);
        if (order is null)
        {
            return Result<ShopeeOrderResponse>.Failure(ShopeeOrderErrors.NotFound);
        }

        ShopeeShopConnection? connection = await connectionRepository.GetByTenantIdAsync(
            request.TenantId, cancellationToken);
        if (connection is null)
        {
            return Result<ShopeeOrderResponse>.Failure(ShopeeOrderErrors.ConnectionNotFound);
        }

        Result ingestResult = await ingestionService.IngestOrderAsync(connection, order.OrderSn, cancellationToken);
        if (ingestResult.IsFailure)
        {
            return Result<ShopeeOrderResponse>.Failure(ingestResult.Error);
        }

        ShopeeOrder? reloaded = await orderRepository.GetByIdAsync(
            request.OrderId, request.TenantId, cancellationToken);
        if (reloaded is null)
        {
            return Result<ShopeeOrderResponse>.Failure(ShopeeOrderErrors.NotFound);
        }

        return Result<ShopeeOrderResponse>.Success(ShopeeOrderResponseMapper.Map(reloaded));
    }
}
