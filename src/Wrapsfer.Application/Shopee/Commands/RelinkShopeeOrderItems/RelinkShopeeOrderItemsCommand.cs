using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Shopee.Responses;

namespace Wrapsfer.Application.Shopee.Commands.RelinkShopeeOrderItems;

public sealed record RelinkShopeeOrderItemsCommand(string TenantId, Guid OrderId) : ICommand<ShopeeOrderResponse>;
