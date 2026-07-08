namespace Wrapsfer.Api.Contracts;

public sealed record LinkShopeeProductRequest(
    Guid ProductId,
    long ShopeeItemId,
    long ShopeeModelId);
