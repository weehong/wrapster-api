using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Shopee.Responses;

namespace Wrapsfer.Application.Shopee.Commands.SyncShopeeProductStock;

public sealed record SyncShopeeProductStockCommand(
    string TenantId,
    Guid? LinkId) : ICommand<ShopeeStockSyncResultResponse>;
