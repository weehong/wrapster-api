using Wrapsfer.Domain.Common;

namespace Wrapsfer.Application.Abstractions.Shopee;

public interface IShopeeGateway
{
    /// <summary>
    /// Builds the Shopee Open Platform shop authorization page URL the seller is
    /// redirected to. Shopee redirects back to <paramref name="redirectUrl"/> with
    /// <c>code</c> and <c>shop_id</c> query parameters after the seller consents.
    /// Shopee's auth_partner flow has no OAuth <c>state</c> passthrough, so the
    /// linking flow relies on Shopee's registered-domain validation of the redirect
    /// plus the tenant-scoped authorization policy on the completion endpoint.
    /// </summary>
    Result<string> BuildShopAuthorizationUrl(string redirectUrl);

    Task<Result<ShopeeTokenGrant>> ExchangeAuthorizationCodeAsync(
        string code,
        long shopId,
        CancellationToken cancellationToken = default);

    Task<Result<ShopeeTokenGrant>> RefreshAccessTokenAsync(
        string refreshToken,
        long shopId,
        CancellationToken cancellationToken = default);

    Task<Result<ShopeeShopProfile>> GetShopProfileAsync(
        long shopId,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<Result<ShopeeItemPage>> GetItemListAsync(
        long shopId,
        string accessToken,
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ShopeeItemDetail>>> GetItemBaseInfoAsync(
        long shopId,
        string accessToken,
        IReadOnlyCollection<long> itemIds,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ShopeeItemModel>>> GetModelListAsync(
        long shopId,
        string accessToken,
        long itemId,
        CancellationToken cancellationToken = default);

    Task<Result> UpdateStockAsync(
        long shopId,
        string accessToken,
        long itemId,
        long modelId,
        int quantity,
        CancellationToken cancellationToken = default);

    Task<Result<ShopeeOrderList>> GetOrderListAsync(
        long shopId,
        string accessToken,
        DateTime updatedFrom,
        DateTime updatedTo,
        string? cursor,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Result<ShopeeOrderDetail>> GetOrderDetailAsync(
        long shopId,
        string accessToken,
        string orderSn,
        CancellationToken cancellationToken = default);

    Task<Result<ShopeeShippingParameter>> GetShippingParameterAsync(
        long shopId,
        string accessToken,
        string orderSn,
        CancellationToken cancellationToken = default);

    Task<Result> ShipOrderAsync(
        long shopId,
        string accessToken,
        ShopeeShipOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<string?>> GetTrackingNumberAsync(
        long shopId,
        string accessToken,
        string orderSn,
        CancellationToken cancellationToken = default);

    /// <summary>Calls create_shipping_document then download_shipping_document; returns the PDF bytes.</summary>
    Task<Result<byte[]>> DownloadShippingDocumentAsync(
        long shopId,
        string accessToken,
        string orderSn,
        CancellationToken cancellationToken = default);
}
