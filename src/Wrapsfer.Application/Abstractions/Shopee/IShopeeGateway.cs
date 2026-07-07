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
}
