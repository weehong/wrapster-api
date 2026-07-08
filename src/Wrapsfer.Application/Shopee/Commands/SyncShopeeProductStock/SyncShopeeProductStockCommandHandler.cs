using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Shopee.Responses;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Domain.Common;

namespace Wrapsfer.Application.Shopee.Commands.SyncShopeeProductStock;

internal sealed class SyncShopeeProductStockCommandHandler(ShopeeStockSyncProcessor processor)
    : ICommandHandler<SyncShopeeProductStockCommand, ShopeeStockSyncResultResponse>
{
    public async Task<Result<ShopeeStockSyncResultResponse>> Handle(
        SyncShopeeProductStockCommand request,
        CancellationToken cancellationToken)
    {
        Result<ShopeeStockSyncRunSummary> result = await processor.RunForTenantAsync(
            request.TenantId, request.LinkId, cancellationToken);
        if (result.IsFailure)
        {
            return Result<ShopeeStockSyncResultResponse>.Failure(result.Error);
        }

        ShopeeStockSyncRunSummary summary = result.Value;
        return Result<ShopeeStockSyncResultResponse>.Success(
            new ShopeeStockSyncResultResponse(
                summary.SyncedCount,
                summary.FailedCount,
                summary.SkippedCount));
    }
}
