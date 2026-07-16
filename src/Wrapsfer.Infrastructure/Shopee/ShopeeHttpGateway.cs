using System.Net.Http.Json;
using System.Text.Json;
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
    private const string GetItemListPath = "/api/v2/product/get_item_list";
    private const string GetItemBaseInfoPath = "/api/v2/product/get_item_base_info";
    private const string GetModelListPath = "/api/v2/product/get_model_list";
    private const string UpdateStockPath = "/api/v2/product/update_stock";
    private const string GetOrderListPath = "/api/v2/order/get_order_list";
    private const string GetOrderDetailPath = "/api/v2/order/get_order_detail";
    private const string GetShippingParameterPath = "/api/v2/logistics/get_shipping_parameter";
    private const string ShipOrderPath = "/api/v2/logistics/ship_order";
    private const string GetTrackingNumberPath = "/api/v2/logistics/get_tracking_number";
    private const string CreateShippingDocumentPath = "/api/v2/logistics/create_shipping_document";
    private const string DownloadShippingDocumentPath = "/api/v2/logistics/download_shipping_document";
    private const string OrderDetailOptionalFields =
        "buyer_username,recipient_address,total_amount,item_list,shipping_carrier,ship_by_date,cod,currency";
    private const string ShippingDocumentExistError = "logistics.shipping_document_exist";
    private const int LoggedBodyMaxLength = 1024;
    private const int BaseInfoChunkSize = 50;

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

    public async Task<Result<ShopeeItemPage>> GetItemListAsync(
        long shopId,
        string accessToken,
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ShopeeOptions shopeeOptions = options.Value;
        if (!shopeeOptions.IsConfigured)
        {
            logger.LogError("Shopee item list requested but the Shopee integration is not configured");
            return Result<ShopeeItemPage>.Failure(ShopeeShopConnectionErrors.NotConfigured);
        }

        string url = BuildShopRequestUrl(shopeeOptions, GetItemListPath, shopId, accessToken) +
                     $"&offset={offset}" +
                     $"&page_size={pageSize}" +
                     "&item_status=NORMAL";

        try
        {
            HttpClient client = httpClientFactory.CreateClient(HttpClientName);
            HttpResponseMessage response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                await LogFailureAsync(response, "item list", shopId, cancellationToken);
                return Result<ShopeeItemPage>.Failure(ShopeeProductLinkErrors.ItemFetchFailed);
            }

            ShopeeItemListResponse? payload =
                await response.Content.ReadFromJsonAsync<ShopeeItemListResponse>(cancellationToken);
            if (payload is null || !string.IsNullOrEmpty(payload.Error) || payload.Response is null)
            {
                LogEnvelopeError("item list", shopId, payload?.Error, payload?.Message, payload?.RequestId);
                return Result<ShopeeItemPage>.Failure(MapShopeeEnvelopeError(payload?.Error,
                    ShopeeProductLinkErrors.ItemFetchFailed));
            }

            List<long> itemIds = (payload.Response.Item ?? [])
                .Where(i => i.ItemId > 0)
                .Select(i => i.ItemId)
                .ToList();

            return Result<ShopeeItemPage>.Success(new ShopeeItemPage(
                itemIds,
                payload.Response.HasNextPage,
                payload.Response.NextOffset,
                payload.Response.TotalCount));
        }
        catch (Exception ex) when (
            ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Shopee item list call failed for shop {ShopId}", shopId);
            return Result<ShopeeItemPage>.Failure(ShopeeProductLinkErrors.ItemFetchFailed);
        }
    }

    public async Task<Result<IReadOnlyList<ShopeeItemDetail>>> GetItemBaseInfoAsync(
        long shopId,
        string accessToken,
        IReadOnlyCollection<long> itemIds,
        CancellationToken cancellationToken = default)
    {
        ShopeeOptions shopeeOptions = options.Value;
        if (!shopeeOptions.IsConfigured)
        {
            logger.LogError("Shopee item base info requested but the Shopee integration is not configured");
            return Result<IReadOnlyList<ShopeeItemDetail>>.Failure(ShopeeShopConnectionErrors.NotConfigured);
        }

        List<ShopeeItemDetail> result = [];
        List<long> remainingIds = itemIds.Where(i => i > 0).Distinct().ToList();
        for (int offset = 0; offset < remainingIds.Count; offset += BaseInfoChunkSize)
        {
            List<long> chunk = remainingIds.Skip(offset).Take(BaseInfoChunkSize).ToList();
            string itemIdList = Uri.EscapeDataString(string.Join(",", chunk));
            string url = BuildShopRequestUrl(shopeeOptions, GetItemBaseInfoPath, shopId, accessToken) +
                         $"&item_id_list={itemIdList}";

            Result<IReadOnlyList<ShopeeItemDetail>> chunkResult = await GetItemBaseInfoChunkAsync(
                url, shopId, cancellationToken);
            if (chunkResult.IsFailure)
            {
                return Result<IReadOnlyList<ShopeeItemDetail>>.Failure(chunkResult.Error);
            }

            result.AddRange(chunkResult.Value);
        }

        return Result<IReadOnlyList<ShopeeItemDetail>>.Success(result);
    }

    public async Task<Result<IReadOnlyList<ShopeeItemModel>>> GetModelListAsync(
        long shopId,
        string accessToken,
        long itemId,
        CancellationToken cancellationToken = default)
    {
        ShopeeOptions shopeeOptions = options.Value;
        if (!shopeeOptions.IsConfigured)
        {
            logger.LogError("Shopee model list requested but the Shopee integration is not configured");
            return Result<IReadOnlyList<ShopeeItemModel>>.Failure(ShopeeShopConnectionErrors.NotConfigured);
        }

        string url = BuildShopRequestUrl(shopeeOptions, GetModelListPath, shopId, accessToken) +
                     $"&item_id={itemId}";

        try
        {
            HttpClient client = httpClientFactory.CreateClient(HttpClientName);
            HttpResponseMessage response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                await LogFailureAsync(response, "model list", shopId, cancellationToken);
                return Result<IReadOnlyList<ShopeeItemModel>>.Failure(ShopeeProductLinkErrors.ItemFetchFailed);
            }

            ShopeeModelListResponse? payload =
                await response.Content.ReadFromJsonAsync<ShopeeModelListResponse>(cancellationToken);
            if (payload is null || !string.IsNullOrEmpty(payload.Error) || payload.Response is null)
            {
                LogEnvelopeError("model list", shopId, payload?.Error, payload?.Message, payload?.RequestId);
                return Result<IReadOnlyList<ShopeeItemModel>>.Failure(MapShopeeEnvelopeError(payload?.Error,
                    ShopeeProductLinkErrors.ItemFetchFailed));
            }

            List<ShopeeItemModel> models = (payload.Response.Model ?? [])
                .Where(m => m.ModelId > 0)
                .Select(m => new ShopeeItemModel(
                    m.ModelId,
                    m.ModelName ?? string.Empty,
                    NullIfWhiteSpace(m.ModelSku),
                    AggregateStock(m.StockInfoV2)))
                .ToList();

            return Result<IReadOnlyList<ShopeeItemModel>>.Success(models);
        }
        catch (Exception ex) when (
            ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Shopee model list call failed for shop {ShopId}", shopId);
            return Result<IReadOnlyList<ShopeeItemModel>>.Failure(ShopeeProductLinkErrors.ItemFetchFailed);
        }
    }

    public async Task<Result> UpdateStockAsync(
        long shopId,
        string accessToken,
        long itemId,
        long modelId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        ShopeeOptions shopeeOptions = options.Value;
        if (!shopeeOptions.IsConfigured)
        {
            logger.LogError("Shopee stock update requested but the Shopee integration is not configured");
            return Result.Failure(ShopeeShopConnectionErrors.NotConfigured);
        }

        ShopeeUpdateStockRequest body = new(
            itemId,
            [new ShopeeUpdateStockStockList(
                modelId,
                [new ShopeeUpdateStockSellerStock(quantity)])]);

        string url = BuildShopRequestUrl(shopeeOptions, UpdateStockPath, shopId, accessToken);

        try
        {
            HttpClient client = httpClientFactory.CreateClient(HttpClientName);
            HttpResponseMessage response = await client.PostAsJsonAsync(url, body, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                await LogFailureAsync(response, "stock update", shopId, cancellationToken);
                return Result.Failure(ShopeeProductLinkErrors.StockPushFailed);
            }

            ShopeeUpdateStockResponse? payload =
                await response.Content.ReadFromJsonAsync<ShopeeUpdateStockResponse>(cancellationToken);
            if (payload is null || !string.IsNullOrEmpty(payload.Error))
            {
                LogEnvelopeError("stock update", shopId, payload?.Error, payload?.Message, payload?.RequestId);
                return Result.Failure(MapShopeeEnvelopeError(payload?.Error,
                    ShopeeProductLinkErrors.StockPushFailed));
            }

            // Shopee can return HTTP 200 with an empty top-level error while individual
            // models are rejected via response.failure_list.
            List<ShopeeUpdateStockFailure>? failures = payload.Response?.FailureList;
            if (failures is { Count: > 0 })
            {
                logger.LogError(
                    "Shopee stock update reported model failures for shop {ShopId}, item {ItemId}: {Reasons} (request {RequestId})",
                    shopId,
                    itemId,
                    string.Join("; ", failures.Select(f => $"model {f.ModelId}: {f.FailedReason}")),
                    payload.RequestId);
                return Result.Failure(ShopeeProductLinkErrors.StockPushFailed);
            }

            return Result.Success();
        }
        catch (Exception ex) when (
            ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Shopee stock update call failed for shop {ShopId}", shopId);
            return Result.Failure(ShopeeProductLinkErrors.StockPushFailed);
        }
    }

    public async Task<Result<ShopeeOrderList>> GetOrderListAsync(
        long shopId,
        string accessToken,
        DateTime updatedFrom,
        DateTime updatedTo,
        string? cursor,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ShopeeOptions shopeeOptions = options.Value;
        if (!shopeeOptions.IsConfigured)
        {
            logger.LogError("Shopee order list requested but the Shopee integration is not configured");
            return Result<ShopeeOrderList>.Failure(ShopeeShopConnectionErrors.NotConfigured);
        }

        long timeFrom = new DateTimeOffset(DateTime.SpecifyKind(updatedFrom, DateTimeKind.Utc)).ToUnixTimeSeconds();
        long timeTo = new DateTimeOffset(DateTime.SpecifyKind(updatedTo, DateTimeKind.Utc)).ToUnixTimeSeconds();

        string url = BuildShopRequestUrl(shopeeOptions, GetOrderListPath, shopId, accessToken) +
                     "&time_range_field=update_time" +
                     $"&time_from={timeFrom}" +
                     $"&time_to={timeTo}" +
                     $"&page_size={pageSize}";

        if (!string.IsNullOrEmpty(cursor))
        {
            url += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        try
        {
            HttpClient client = httpClientFactory.CreateClient(HttpClientName);
            HttpResponseMessage response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                await LogFailureAsync(response, "order list", shopId, cancellationToken);
                return Result<ShopeeOrderList>.Failure(ShopeeOrderErrors.OrderListFetchFailed);
            }

            ShopeeGetOrderListResponse? payload =
                await response.Content.ReadFromJsonAsync<ShopeeGetOrderListResponse>(cancellationToken);
            if (payload is null || !string.IsNullOrEmpty(payload.Error) || payload.Response is null)
            {
                LogEnvelopeError("order list", shopId, payload?.Error, payload?.Message, payload?.RequestId);
                return Result<ShopeeOrderList>.Failure(ShopeeOrderErrors.OrderListFetchFailed);
            }

            List<string> orderSns = (payload.Response.OrderList ?? [])
                .Where(o => !string.IsNullOrWhiteSpace(o.OrderSn))
                .Select(o => o.OrderSn!)
                .ToList();

            return Result<ShopeeOrderList>.Success(new ShopeeOrderList(
                orderSns, payload.Response.More, NullIfWhiteSpace(payload.Response.NextCursor)));
        }
        catch (Exception ex) when (
            ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Shopee order list call failed for shop {ShopId}", shopId);
            return Result<ShopeeOrderList>.Failure(ShopeeOrderErrors.OrderListFetchFailed);
        }
    }

    public async Task<Result<ShopeeOrderDetail>> GetOrderDetailAsync(
        long shopId,
        string accessToken,
        string orderSn,
        CancellationToken cancellationToken = default)
    {
        ShopeeOptions shopeeOptions = options.Value;
        if (!shopeeOptions.IsConfigured)
        {
            logger.LogError("Shopee order detail requested but the Shopee integration is not configured");
            return Result<ShopeeOrderDetail>.Failure(ShopeeShopConnectionErrors.NotConfigured);
        }

        string url = BuildShopRequestUrl(shopeeOptions, GetOrderDetailPath, shopId, accessToken) +
                     $"&order_sn_list={Uri.EscapeDataString(orderSn)}" +
                     $"&response_optional_fields={OrderDetailOptionalFields}";

        try
        {
            HttpClient client = httpClientFactory.CreateClient(HttpClientName);
            HttpResponseMessage response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                await LogFailureAsync(response, "order detail", shopId, cancellationToken);
                return Result<ShopeeOrderDetail>.Failure(ShopeeOrderErrors.OrderDetailFetchFailed);
            }

            ShopeeGetOrderDetailResponse? payload =
                await response.Content.ReadFromJsonAsync<ShopeeGetOrderDetailResponse>(cancellationToken);
            ShopeeOrderDetailEntry? entry = payload?.Response?.OrderList?.FirstOrDefault();
            if (payload is null || !string.IsNullOrEmpty(payload.Error) || entry is null)
            {
                LogEnvelopeError("order detail", shopId, payload?.Error, payload?.Message, payload?.RequestId);
                return Result<ShopeeOrderDetail>.Failure(ShopeeOrderErrors.OrderDetailFetchFailed);
            }

            List<ShopeeOrderDetailItem> items = (entry.ItemList ?? [])
                .Select(i => new ShopeeOrderDetailItem(
                    i.ItemId,
                    i.ModelId,
                    i.ItemName,
                    i.ModelName,
                    NullIfWhiteSpace(i.ItemSku) ?? NullIfWhiteSpace(i.ModelSku),
                    i.ModelQuantityPurchased))
                .ToList();

            DateTime? shipByDate = entry.ShipByDate is long shipBy && shipBy > 0
                ? DateTimeOffset.FromUnixTimeSeconds(shipBy).UtcDateTime
                : null;

            decimal? codAmount = entry.Cod ? entry.TotalAmount : null;

            return Result<ShopeeOrderDetail>.Success(new ShopeeOrderDetail(
                entry.OrderSn,
                entry.OrderStatus ?? string.Empty,
                entry.Region,
                entry.BuyerUsername,
                entry.RecipientAddress?.Name,
                entry.RecipientAddress?.Phone,
                entry.RecipientAddress?.FullAddress,
                entry.TotalAmount,
                entry.Currency,
                codAmount,
                entry.ShippingCarrier,
                shipByDate,
                items));
        }
        catch (Exception ex) when (
            ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Shopee order detail call failed for shop {ShopId}", shopId);
            return Result<ShopeeOrderDetail>.Failure(ShopeeOrderErrors.OrderDetailFetchFailed);
        }
    }

    public async Task<Result<ShopeeShippingParameter>> GetShippingParameterAsync(
        long shopId,
        string accessToken,
        string orderSn,
        CancellationToken cancellationToken = default)
    {
        ShopeeOptions shopeeOptions = options.Value;
        if (!shopeeOptions.IsConfigured)
        {
            logger.LogError("Shopee shipping parameter requested but the Shopee integration is not configured");
            return Result<ShopeeShippingParameter>.Failure(ShopeeShopConnectionErrors.NotConfigured);
        }

        string url = BuildShopRequestUrl(shopeeOptions, GetShippingParameterPath, shopId, accessToken) +
                     $"&order_sn={Uri.EscapeDataString(orderSn)}";

        try
        {
            HttpClient client = httpClientFactory.CreateClient(HttpClientName);
            HttpResponseMessage response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                await LogFailureAsync(response, "shipping parameter", shopId, cancellationToken);
                return Result<ShopeeShippingParameter>.Failure(ShopeeOrderErrors.ShippingParameterFetchFailed);
            }

            ShopeeGetShippingParameterResponse? payload =
                await response.Content.ReadFromJsonAsync<ShopeeGetShippingParameterResponse>(cancellationToken);
            if (payload is null || !string.IsNullOrEmpty(payload.Error) || payload.Response is null)
            {
                LogEnvelopeError("shipping parameter", shopId, payload?.Error, payload?.Message, payload?.RequestId);
                return Result<ShopeeShippingParameter>.Failure(ShopeeOrderErrors.ShippingParameterFetchFailed);
            }

            bool supportsPickup = payload.Response.InfoNeeded?.Pickup is not null;
            bool supportsDropoff = payload.Response.InfoNeeded?.Dropoff is not null;

            List<ShopeePickupAddress> pickupAddresses = (payload.Response.Pickup?.AddressList ?? [])
                .Select(a => new ShopeePickupAddress(
                    a.AddressId,
                    a.Address ?? string.Empty,
                    (a.TimeSlotList ?? [])
                        .Select(t => new ShopeePickupTimeSlot(
                            t.PickupTimeId ?? string.Empty,
                            DateTimeOffset.FromUnixTimeSeconds(t.Date).UtcDateTime,
                            t.TimeText))
                        .ToList()))
                .ToList();

            List<ShopeeDropoffBranch> dropoffBranches = (payload.Response.Dropoff?.BranchList ?? [])
                .Select(b => new ShopeeDropoffBranch(b.BranchId, b.Address ?? string.Empty))
                .ToList();

            return Result<ShopeeShippingParameter>.Success(new ShopeeShippingParameter(
                supportsPickup, supportsDropoff, pickupAddresses, dropoffBranches));
        }
        catch (Exception ex) when (
            ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Shopee shipping parameter call failed for shop {ShopId}", shopId);
            return Result<ShopeeShippingParameter>.Failure(ShopeeOrderErrors.ShippingParameterFetchFailed);
        }
    }

    public async Task<Result> ShipOrderAsync(
        long shopId,
        string accessToken,
        ShopeeShipOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ShopeeOptions shopeeOptions = options.Value;
        if (!shopeeOptions.IsConfigured)
        {
            logger.LogError("Shopee ship order requested but the Shopee integration is not configured");
            return Result.Failure(ShopeeShopConnectionErrors.NotConfigured);
        }

        ShopeeShipOrderApiRequest body = new(
            request.OrderSn,
            request.Pickup is null
                ? null
                : new ShopeeShipOrderPickupBody(request.Pickup.AddressId, request.Pickup.PickupTimeId),
            request.Dropoff is null ? null : new ShopeeShipOrderDropoffBody(request.Dropoff.BranchId));

        string url = BuildShopRequestUrl(shopeeOptions, ShipOrderPath, shopId, accessToken);

        try
        {
            HttpClient client = httpClientFactory.CreateClient(HttpClientName);
            HttpResponseMessage response = await client.PostAsJsonAsync(url, body, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                await LogFailureAsync(response, "ship order", shopId, cancellationToken);
                return Result.Failure(ShopeeOrderErrors.ShipmentRequestFailed);
            }

            ShopeeEnvelopeResponse? payload =
                await response.Content.ReadFromJsonAsync<ShopeeEnvelopeResponse>(cancellationToken);
            if (payload is null || !string.IsNullOrEmpty(payload.Error))
            {
                LogEnvelopeError("ship order", shopId, payload?.Error, payload?.Message, payload?.RequestId);
                return Result.Failure(ShopeeOrderErrors.ShipmentRequestFailed);
            }

            return Result.Success();
        }
        catch (Exception ex) when (
            ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Shopee ship order call failed for shop {ShopId}", shopId);
            return Result.Failure(ShopeeOrderErrors.ShipmentRequestFailed);
        }
    }

    public async Task<Result<string?>> GetTrackingNumberAsync(
        long shopId,
        string accessToken,
        string orderSn,
        CancellationToken cancellationToken = default)
    {
        ShopeeOptions shopeeOptions = options.Value;
        if (!shopeeOptions.IsConfigured)
        {
            logger.LogError("Shopee tracking number requested but the Shopee integration is not configured");
            return Result<string?>.Failure(ShopeeShopConnectionErrors.NotConfigured);
        }

        string url = BuildShopRequestUrl(shopeeOptions, GetTrackingNumberPath, shopId, accessToken) +
                     $"&order_sn={Uri.EscapeDataString(orderSn)}";

        try
        {
            HttpClient client = httpClientFactory.CreateClient(HttpClientName);
            HttpResponseMessage response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                await LogFailureAsync(response, "tracking number", shopId, cancellationToken);
                return Result<string?>.Failure(ShopeeOrderErrors.TrackingNumberFetchFailed);
            }

            ShopeeGetTrackingNumberResponse? payload =
                await response.Content.ReadFromJsonAsync<ShopeeGetTrackingNumberResponse>(cancellationToken);
            if (payload is null || !string.IsNullOrEmpty(payload.Error))
            {
                LogEnvelopeError("tracking number", shopId, payload?.Error, payload?.Message, payload?.RequestId);
                return Result<string?>.Failure(ShopeeOrderErrors.TrackingNumberFetchFailed);
            }

            // An empty or missing tracking number is a valid state (not yet assigned by
            // Shopee), not a failure.
            return Result<string?>.Success(NullIfWhiteSpace(payload.Response?.TrackingNumber));
        }
        catch (Exception ex) when (
            ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Shopee tracking number call failed for shop {ShopId}", shopId);
            return Result<string?>.Failure(ShopeeOrderErrors.TrackingNumberFetchFailed);
        }
    }

    public async Task<Result<byte[]>> DownloadShippingDocumentAsync(
        long shopId,
        string accessToken,
        string orderSn,
        CancellationToken cancellationToken = default)
    {
        ShopeeOptions shopeeOptions = options.Value;
        if (!shopeeOptions.IsConfigured)
        {
            logger.LogError("Shopee shipping document requested but the Shopee integration is not configured");
            return Result<byte[]>.Failure(ShopeeShopConnectionErrors.NotConfigured);
        }

        ShopeeShippingDocumentOrderRequest body = new([new ShopeeShippingDocumentOrderEntry(orderSn)]);

        try
        {
            HttpClient client = httpClientFactory.CreateClient(HttpClientName);

            string createUrl = BuildShopRequestUrl(shopeeOptions, CreateShippingDocumentPath, shopId, accessToken);
            HttpResponseMessage createResponse = await client.PostAsJsonAsync(createUrl, body, cancellationToken);
            if (!createResponse.IsSuccessStatusCode)
            {
                await LogFailureAsync(createResponse, "create shipping document", shopId, cancellationToken);
                return Result<byte[]>.Failure(ShopeeOrderErrors.LabelFetchFailed);
            }

            ShopeeEnvelopeResponse? createPayload =
                await createResponse.Content.ReadFromJsonAsync<ShopeeEnvelopeResponse>(cancellationToken);
            bool documentAlreadyExists = string.Equals(
                createPayload?.Error, ShippingDocumentExistError, StringComparison.OrdinalIgnoreCase);
            if (createPayload is null || (!string.IsNullOrEmpty(createPayload.Error) && !documentAlreadyExists))
            {
                LogEnvelopeError(
                    "create shipping document", shopId, createPayload?.Error, createPayload?.Message,
                    createPayload?.RequestId);
                return Result<byte[]>.Failure(ShopeeOrderErrors.LabelFetchFailed);
            }

            string downloadUrl = BuildShopRequestUrl(
                shopeeOptions, DownloadShippingDocumentPath, shopId, accessToken);
            HttpResponseMessage downloadResponse = await client.PostAsJsonAsync(downloadUrl, body, cancellationToken);
            if (!downloadResponse.IsSuccessStatusCode)
            {
                await LogFailureAsync(downloadResponse, "download shipping document", shopId, cancellationToken);
                return Result<byte[]>.Failure(ShopeeOrderErrors.LabelFetchFailed);
            }

            string? contentType = downloadResponse.Content.Headers.ContentType?.MediaType;
            byte[] documentBytes = await downloadResponse.Content.ReadAsByteArrayAsync(cancellationToken);

            // Shopee reports download failures as a JSON error envelope over HTTP 200;
            // a successful download returns the PDF bytes with a binary content type.
            if (contentType is not null && contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
            {
                ShopeeEnvelopeResponse? downloadPayload =
                    JsonSerializer.Deserialize<ShopeeEnvelopeResponse>(documentBytes);
                LogEnvelopeError(
                    "download shipping document", shopId, downloadPayload?.Error, downloadPayload?.Message,
                    downloadPayload?.RequestId);
                return Result<byte[]>.Failure(ShopeeOrderErrors.LabelFetchFailed);
            }

            return Result<byte[]>.Success(documentBytes);
        }
        catch (Exception ex) when (
            ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Shopee shipping document call failed for shop {ShopId}", shopId);
            return Result<byte[]>.Failure(ShopeeOrderErrors.LabelFetchFailed);
        }
    }

    private async Task<Result<IReadOnlyList<ShopeeItemDetail>>> GetItemBaseInfoChunkAsync(
        string url,
        long shopId,
        CancellationToken cancellationToken)
    {
        try
        {
            HttpClient client = httpClientFactory.CreateClient(HttpClientName);
            HttpResponseMessage response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                await LogFailureAsync(response, "item base info", shopId, cancellationToken);
                return Result<IReadOnlyList<ShopeeItemDetail>>.Failure(ShopeeProductLinkErrors.ItemFetchFailed);
            }

            ShopeeItemBaseInfoResponse? payload =
                await response.Content.ReadFromJsonAsync<ShopeeItemBaseInfoResponse>(cancellationToken);
            if (payload is null || !string.IsNullOrEmpty(payload.Error) || payload.Response is null)
            {
                LogEnvelopeError("item base info", shopId, payload?.Error, payload?.Message, payload?.RequestId);
                return Result<IReadOnlyList<ShopeeItemDetail>>.Failure(MapShopeeEnvelopeError(payload?.Error,
                    ShopeeProductLinkErrors.ItemFetchFailed));
            }

            List<ShopeeItemDetail> items = (payload.Response.ItemList ?? [])
                .Where(i => i.ItemId > 0)
                .Select(i => new ShopeeItemDetail(
                    i.ItemId,
                    i.ItemName ?? string.Empty,
                    NullIfWhiteSpace(i.ItemSku),
                    i.ItemStatus ?? string.Empty,
                    i.HasModel,
                    i.HasModel ? null : AggregateStock(i.StockInfoV2),
                    FirstImageUrl(i)))
                .ToList();

            return Result<IReadOnlyList<ShopeeItemDetail>>.Success(items);
        }
        catch (Exception ex) when (
            ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Shopee item base info call failed for shop {ShopId}", shopId);
            return Result<IReadOnlyList<ShopeeItemDetail>>.Failure(ShopeeProductLinkErrors.ItemFetchFailed);
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

    private static string BuildShopRequestUrl(
        ShopeeOptions shopeeOptions,
        string apiPath,
        long shopId,
        string accessToken)
    {
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string sign = ShopeeRequestSigner.SignShopRequest(
            shopeeOptions.PartnerKey, shopeeOptions.PartnerId, apiPath, timestamp, accessToken, shopId);

        return $"{shopeeOptions.BaseUrl.TrimEnd('/')}{apiPath}" +
               $"?partner_id={shopeeOptions.PartnerId}" +
               $"&timestamp={timestamp}" +
               $"&access_token={Uri.EscapeDataString(accessToken)}" +
               $"&shop_id={shopId}" +
               $"&sign={sign}";
    }

    private void LogEnvelopeError(
        string operation,
        long shopId,
        string? error,
        string? message,
        string? requestId) =>
        logger.LogError(
            "Shopee {Operation} for shop {ShopId} returned error {Error}: {Message} (request {RequestId})",
            operation, shopId, error, message, requestId);

    private static Error MapShopeeEnvelopeError(string? error, Error fallback)
    {
        if (IsShopeeAuthError(error))
        {
            return ShopeeProductLinkErrors.AuthFailed;
        }

        return fallback;
    }

    private static bool IsShopeeAuthError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return false;
        }

        string normalized = error.Trim().ToLowerInvariant();
        return normalized is "error_auth" or "error_token" or "invalid_access_token";
    }

    private static int? AggregateStock(ShopeeStockInfoV2? stockInfo)
    {
        if (stockInfo?.SummaryInfo?.TotalAvailableStock is int totalAvailableStock)
        {
            return totalAvailableStock;
        }

        if (stockInfo?.SellerStock is null || stockInfo.SellerStock.Count == 0)
        {
            return null;
        }

        return stockInfo.SellerStock.Sum(s => s.Stock ?? 0);
    }

    private static string? FirstImageUrl(ShopeeItemBaseInfoItem item)
    {
        string? imageUrl = item.ImageInfo?.ImageUrlList?.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            return imageUrl;
        }

        return item.Image?.ImageUrlList?.FirstOrDefault();
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static string Truncate(string value) =>
        string.IsNullOrEmpty(value) || value.Length <= LoggedBodyMaxLength
            ? value
            : value[..LoggedBodyMaxLength];
}
