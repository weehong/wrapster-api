namespace Wrapsfer.Application.Shopee.Responses;

public sealed record ShopeeOrdersResponse(
    IReadOnlyList<ShopeeOrderResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
