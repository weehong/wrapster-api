using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Shopee.Responses;

namespace Wrapsfer.Application.Shopee.Commands.LinkShopeeProduct;

public sealed record LinkShopeeProductCommand(
    string TenantId,
    Guid ProductId,
    long ShopeeItemId,
    long ShopeeModelId) : ICommand<ShopeeProductLinkResponse>;
