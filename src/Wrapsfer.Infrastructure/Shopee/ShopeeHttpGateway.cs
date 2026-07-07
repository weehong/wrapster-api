using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Infrastructure.Shopee.Dtos;

namespace Wrapsfer.Infrastructure.Shopee;

/// <summary>
/// Shopee Open Platform v2 client. Every request carries partner_id, a unix
/// timestamp, and an HMAC-SHA256 signature; Shopee reports failures via an
/// <c>error</c> field in the body even on HTTP 200, so both are checked.
/// Response bodies are logged server-side only and never surfaced to callers.
/// </summary>
internal sealed class ShopeeHttpGateway(
    IHttpClientFactory httpClientFactory,
    IOptions<ShopeeOptions> options,
    ILogger<ShopeeHttpGateway> logger) : IShopeeGateway
{
    internal const string HttpClientName = "Shopee";

    private const string AuthPartnerPath = "/api/v2/shop/auth_partner";
    private const string GetTokenPath = "/api/v2/auth/token/get";
    private const string RefreshTokenPath = "/api/v2/auth/access_token/get";
    private const string GetShopInfoPath = "/api/v2/shop/get_shop_info";
    private const int LoggedBodyMaxLength = 1024;

    public Result<string> BuildShopAuthorizationUrl(string redirectUrl)
    {
        ShopeeOptions shopeeOptions = options.Value;
        if (!shopeeOptions.IsConfigured)
        {
            logger.LogError("Shopee authorization URL requested but the Shopee integration is not configured");
            return Result<string>.Failure(ShopeeShopConnectionErrors.NotConfigured);
        }

        if (!Uri.TryCreate(redirectUrl, UriKind.Absolute, out Uri? redirectUri)
            || (redirectUri.Scheme != Uri.UriSchemeHttp && redirectUri.Scheme != Uri.UriSchemeHttps))
        {
            return Result<string>.Failure(ShopeeShopConnectionErrors.InvalidRedirectUrl);
        }

        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string sign = ShopeeRequestSigner.SignPublicRequest(
            shopeeOptions.PartnerKey, shopeeOptions.PartnerId, AuthPartnerPath, timestamp);

        string url = $"{shopeeOptions.BaseUrl.TrimEnd('/')}{AuthPartnerPath}" +
                     $"?partner_id={shopeeOptions.PartnerId}" +
                     $"&timestamp={timestamp}" +
                     $"&sign={sign}" +
                     $"&redirect={Uri.EscapeDataString(redirectUrl)}";

        return Result<string>.Success(url);
    }

    public async Task<Result<ShopeeTokenGrant>> ExchangeAuthorizationCodeAsync(
        string code,
        long shopId,
        CancellationToken cancellationToken = default)
    {
        ShopeeOptions shopeeOptions = options.Value;
        if (!shopeeOptions.IsConfigured)
        {
            logger.LogError("Shopee code exchange requested but the Shopee integration is not configured");
            return Result<ShopeeTokenGrant>.Failure(ShopeeShopConnectionErrors.NotConfigured);
        }

        ShopeeGetTokenRequest body = new(code, shopId, shopeeOptions.PartnerId);
        return await PostForTokenGrantAsync(
            GetTokenPath, body, "code exchange", ShopeeShopConnectionErrors.AuthorizationExchangeFailed,
            shopId, cancellationToken);
    }

    public async Task<Result<ShopeeTokenGrant>> RefreshAccessTokenAsync(
        string refreshToken,
        long shopId,
        CancellationToken cancellationToken = default)
    {
        ShopeeOptions shopeeOptions = options.Value;
        if (!shopeeOptions.IsConfigured)
        {
            logger.LogError("Shopee token refresh requested but the Shopee integration is not configured");
            return Result<ShopeeTokenGrant>.Failure(ShopeeShopConnectionErrors.NotConfigured);
        }

        ShopeeRefreshTokenRequest body = new(refreshToken, shopId, shopeeOptions.PartnerId);
        return await PostForTokenGrantAsync(
            RefreshTokenPath, body, "token refresh", ShopeeShopConnectionErrors.TokenRefreshFailed,
            shopId, cancellationToken);
    }

    public async Task<Result<ShopeeShopProfile>> GetShopProfileAsync(
        long shopId,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        ShopeeOptions shopeeOptions = options.Value;
        if (!shopeeOptions.IsConfigured)
        {
            logger.LogError("Shopee shop profile requested but the Shopee integration is not configured");
            return Result<ShopeeShopProfile>.Failure(ShopeeShopConnectionErrors.NotConfigured);
        }

        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string sign = ShopeeRequestSigner.SignShopRequest(
            shopeeOptions.PartnerKey, shopeeOptions.PartnerId, GetShopInfoPath, timestamp, accessToken, shopId);

        string url = $"{shopeeOptions.BaseUrl.TrimEnd('/')}{GetShopInfoPath}" +
                     $"?partner_id={shopeeOptions.PartnerId}" +
                     $"&timestamp={timestamp}" +
                     $"&access_token={Uri.EscapeDataString(accessToken)}" +
                     $"&shop_id={shopId}" +
                     $"&sign={sign}";

        try
        {
            HttpClient client = httpClientFactory.CreateClient(HttpClientName);
            HttpResponseMessage response = await client.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                await LogFailureAsync(response, "shop profile", shopId, cancellationToken);
                return Result<ShopeeShopProfile>.Failure(ShopeeShopConnectionErrors.ShopProfileFetchFailed);
            }

            ShopeeShopInfoResponse? info =
                await response.Content.ReadFromJsonAsync<ShopeeShopInfoResponse>(cancellationToken);

            if (info is null || !string.IsNullOrEmpty(info.Error))
            {
                logger.LogError(
                    "Shopee shop profile call for shop {ShopId} returned error {Error}: {Message} (request {RequestId})",
                    shopId, info?.Error, info?.Message, info?.RequestId);
                return Result<ShopeeShopProfile>.Failure(ShopeeShopConnectionErrors.ShopProfileFetchFailed);
            }

            return Result<ShopeeShopProfile>.Success(new ShopeeShopProfile(info.ShopName, info.Region));
        }
        // HttpClient.Timeout also surfaces as OperationCanceledException; only caller
        // cancellation is allowed to escape — timeouts become Result failures.
        catch (Exception ex) when (
            ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Shopee shop profile call failed for shop {ShopId}", shopId);
            return Result<ShopeeShopProfile>.Failure(ShopeeShopConnectionErrors.ShopProfileFetchFailed);
        }
    }

    private async Task<Result<ShopeeTokenGrant>> PostForTokenGrantAsync<TRequest>(
        string apiPath,
        TRequest body,
        string operation,
        Error failureError,
        long shopId,
        CancellationToken cancellationToken)
    {
        ShopeeOptions shopeeOptions = options.Value;
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string sign = ShopeeRequestSigner.SignPublicRequest(
            shopeeOptions.PartnerKey, shopeeOptions.PartnerId, apiPath, timestamp);

        string url = $"{shopeeOptions.BaseUrl.TrimEnd('/')}{apiPath}" +
                     $"?partner_id={shopeeOptions.PartnerId}" +
                     $"&timestamp={timestamp}" +
                     $"&sign={sign}";

        try
        {
            HttpClient client = httpClientFactory.CreateClient(HttpClientName);
            HttpResponseMessage response = await client.PostAsJsonAsync(url, body, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                await LogFailureAsync(response, operation, shopId, cancellationToken);
                return Result<ShopeeTokenGrant>.Failure(failureError);
            }

            ShopeeTokenResponse? token =
                await response.Content.ReadFromJsonAsync<ShopeeTokenResponse>(cancellationToken);

            if (token is null
                || !string.IsNullOrEmpty(token.Error)
                || string.IsNullOrWhiteSpace(token.AccessToken)
                || string.IsNullOrWhiteSpace(token.RefreshToken)
                || token.ExpireIn <= 0)
            {
                logger.LogError(
                    "Shopee {Operation} for shop {ShopId} returned error {Error}: {Message} (request {RequestId})",
                    operation, shopId, token?.Error, token?.Message, token?.RequestId);
                return Result<ShopeeTokenGrant>.Failure(failureError);
            }

            return Result<ShopeeTokenGrant>.Success(
                new ShopeeTokenGrant(token.AccessToken, token.RefreshToken, token.ExpireIn));
        }
        // HttpClient.Timeout also surfaces as OperationCanceledException; only caller
        // cancellation is allowed to escape — timeouts become Result failures.
        catch (Exception ex) when (
            ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Shopee {Operation} call failed for shop {ShopId}", operation, shopId);
            return Result<ShopeeTokenGrant>.Failure(failureError);
        }
    }

    private async Task LogFailureAsync(
        HttpResponseMessage response,
        string operation,
        long shopId,
        CancellationToken cancellationToken)
    {
        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogError(
            "Shopee {Operation} for shop {ShopId} failed: {Status} {Body}",
            operation, shopId, response.StatusCode, Truncate(responseBody));
    }

    private static string Truncate(string value) =>
        string.IsNullOrEmpty(value) || value.Length <= LoggedBodyMaxLength
            ? value
            : value[..LoggedBodyMaxLength];
}
