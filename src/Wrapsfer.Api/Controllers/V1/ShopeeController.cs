using System.Globalization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Wrapsfer.Api.Contracts;
using Wrapsfer.Application.Shopee.Commands.CompleteShopeeAuthorization;
using Wrapsfer.Application.Shopee.Commands.CreateShopeeLinkedProduct;
using Wrapsfer.Application.Shopee.Commands.DisconnectShopeeShop;
using Wrapsfer.Application.Shopee.Commands.IngestShopeeWebhook;
using Wrapsfer.Application.Shopee.Commands.LinkShopeeProduct;
using Wrapsfer.Application.Shopee.Commands.RelinkShopeeOrderItems;
using Wrapsfer.Application.Shopee.Commands.ShipShopeeOrder;
using Wrapsfer.Application.Shopee.Commands.SyncShopeeProductStock;
using Wrapsfer.Application.Shopee.Commands.UnlinkShopeeProduct;
using Wrapsfer.Application.Shopee.Queries.GetShopeeAuthorizationLink;
using Wrapsfer.Application.Shopee.Queries.GetShopeeConnection;
using Wrapsfer.Application.Shopee.Queries.GetShopeeOrderDetail;
using Wrapsfer.Application.Shopee.Queries.GetShopeeOrderLabel;
using Wrapsfer.Application.Shopee.Queries.GetShopeeOrders;
using Wrapsfer.Application.Shopee.Queries.GetShopeeShippingParameter;
using Wrapsfer.Application.Shopee.Queries.GetShopeeShopItems;
using Wrapsfer.Application.Shopee.Queries.ListShopeeProductLinks;
using Wrapsfer.Application.Shopee.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Infrastructure.Authentication;
using Wrapsfer.Infrastructure.Shopee;

namespace Wrapsfer.Api.Controllers.V1;

[Authorize(Policy = AuthorizationPolicies.PartnerIntegrationAdmin)]
[Route("api/v{version:apiVersion}/shopee")]
public sealed class ShopeeController(
    ISender sender,
    IOptions<ShopeeOptions> shopeeOptions,
    ILogger<ShopeeController> logger) : ApiControllerBase
{
    // Shopee only redirects to the domain registered in its console, which is the API's public
    // domain — so this anonymous endpoint receives the redirect and relays the browser (with
    // code and shop_id intact) to the frontend page that completes the authorization.
    [AllowAnonymous]
    [HttpGet("callback")]
    public IActionResult AuthorizationCallback(
        [FromQuery] string? code,
        [FromQuery(Name = "shop_id")] long? shopId)
    {
        ShopeeOptions options = shopeeOptions.Value;
        if (string.IsNullOrWhiteSpace(options.FrontendBaseUrl))
        {
            logger.LogError(
                "Shopee authorization callback received but Shopee:FrontendBaseUrl is not configured");
            return Problem(statusCode: StatusCodes.Status500InternalServerError);
        }

        Dictionary<string, string?> query = new();
        if (!string.IsNullOrEmpty(code))
        {
            query["code"] = code;
        }
        if (shopId is not null)
        {
            query["shop_id"] = shopId.Value.ToString(CultureInfo.InvariantCulture);
        }

        string frontendCallbackUrl = QueryHelpers.AddQueryString(
            $"{options.FrontendBaseUrl.TrimEnd('/')}{options.AuthorizationCallbackPath}",
            query);
        return Redirect(frontendCallbackUrl);
    }

    // Shopee push mechanism. Anonymous by necessity; authenticity is the HMAC signature over
    // the registered push URL + raw body. Must ack fast — Shopee disables slow endpoints —
    // so this only verifies, stores, and returns; processing happens in ShopeeWebhookDispatchJob.
    [AllowAnonymous]
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook(CancellationToken cancellationToken)
    {
        using StreamReader reader = new(Request.Body);
        string body = await reader.ReadToEndAsync(cancellationToken);
        string authorizationHeader = Request.Headers.Authorization.ToString();

        Result result = await sender.Send(
            new IngestShopeeWebhookCommand(body, authorizationHeader), cancellationToken);
        if (result.IsSuccess)
        {
            return Ok();
        }

        return result.Error.Code == ShopeeWebhookEventErrors.InvalidSignature.Code
            ? Unauthorized()
            : BadRequest();
    }

    [HttpGet("{tenantId}/connection")]
    public async Task<IActionResult> GetConnection(string tenantId, CancellationToken cancellationToken)
    {
        Result<ShopeeConnectionResponse> result =
            await sender.Send(new GetShopeeConnectionQuery(tenantId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{tenantId}/connection/authorization-link")]
    public async Task<IActionResult> CreateAuthorizationLink(
        string tenantId,
        [FromBody] CreateShopeeAuthorizationLinkRequest request,
        CancellationToken cancellationToken)
    {
        Result<ShopeeAuthorizationLinkResponse> result = await sender.Send(
            new GetShopeeAuthorizationLinkQuery(tenantId, request.RedirectUrl), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{tenantId}/connection")]
    public async Task<IActionResult> CompleteAuthorization(
        string tenantId,
        [FromBody] CompleteShopeeAuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        Result<ShopeeConnectionResponse> result = await sender.Send(
            new CompleteShopeeAuthorizationCommand(tenantId, request.Code, request.ShopId), cancellationToken);
        return ToCreatedResult(result);
    }

    [HttpDelete("{tenantId}/connection")]
    public async Task<IActionResult> Disconnect(string tenantId, CancellationToken cancellationToken)
    {
        Result result = await sender.Send(new DisconnectShopeeShopCommand(tenantId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{tenantId}/items")]
    public async Task<IActionResult> GetItems(
        string tenantId,
        [FromQuery] int offset,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        int clampedPageSize = Math.Clamp(pageSize, 1, 20);
        Result<ShopeeShopItemsResponse> result = await sender.Send(
            new GetShopeeShopItemsQuery(tenantId, Math.Max(offset, 0), clampedPageSize), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{tenantId}/product-links")]
    public async Task<IActionResult> ListProductLinks(
        string tenantId,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<ShopeeProductLinkResponse>> result = await sender.Send(
            new ListShopeeProductLinksQuery(tenantId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{tenantId}/product-links")]
    public async Task<IActionResult> LinkProduct(
        string tenantId,
        [FromBody] LinkShopeeProductRequest request,
        CancellationToken cancellationToken)
    {
        Result<ShopeeProductLinkResponse> result = await sender.Send(
            new LinkShopeeProductCommand(
                tenantId,
                request.ProductId,
                request.ShopeeItemId,
                request.ShopeeModelId),
            cancellationToken);
        return ToCreatedResult(result);
    }

    [HttpPost("{tenantId}/product-links/with-new-product")]
    public async Task<IActionResult> CreateLinkedProduct(
        string tenantId,
        [FromBody] CreateShopeeLinkedProductRequest request,
        CancellationToken cancellationToken)
    {
        Result<ShopeeProductLinkResponse> result = await sender.Send(
            new CreateShopeeLinkedProductCommand(
                tenantId,
                request.ShopeeItemId,
                request.ShopeeModelId,
                request.Barcode,
                request.Name,
                request.SkuCode,
                request.Cost,
                request.StockQuantity,
                request.LowStockThreshold),
            cancellationToken);
        return ToCreatedResult(result);
    }

    [HttpDelete("{tenantId}/product-links/{linkId:guid}")]
    public async Task<IActionResult> UnlinkProduct(
        string tenantId,
        Guid linkId,
        CancellationToken cancellationToken)
    {
        Result result = await sender.Send(
            new UnlinkShopeeProductCommand(tenantId, linkId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{tenantId}/product-links/sync")]
    public async Task<IActionResult> SyncAllProductLinks(
        string tenantId,
        CancellationToken cancellationToken)
    {
        Result<ShopeeStockSyncResultResponse> result = await sender.Send(
            new SyncShopeeProductStockCommand(tenantId, null), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{tenantId}/product-links/{linkId:guid}/sync")]
    public async Task<IActionResult> SyncProductLink(
        string tenantId,
        Guid linkId,
        CancellationToken cancellationToken)
    {
        Result<ShopeeStockSyncResultResponse> result = await sender.Send(
            new SyncShopeeProductStockCommand(tenantId, linkId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{tenantId}/orders")]
    public async Task<IActionResult> ListOrders(
        string tenantId,
        [FromQuery] ShopeeOrderStatus? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        Result<ShopeeOrdersResponse> result = await sender.Send(
            new GetShopeeOrdersQuery(tenantId, status, search, Math.Max(page, 1),
                Math.Clamp(pageSize, 1, 100)),
            cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{tenantId}/orders/{orderId:guid}")]
    public async Task<IActionResult> GetOrder(
        string tenantId, Guid orderId, CancellationToken cancellationToken)
    {
        Result<ShopeeOrderResponse> result = await sender.Send(
            new GetShopeeOrderDetailQuery(tenantId, orderId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{tenantId}/orders/{orderId:guid}/shipping-parameter")]
    public async Task<IActionResult> GetShippingParameter(
        string tenantId, Guid orderId, CancellationToken cancellationToken)
    {
        Result<ShopeeShippingParameterResponse> result = await sender.Send(
            new GetShopeeShippingParameterQuery(tenantId, orderId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{tenantId}/orders/{orderId:guid}/ship")]
    public async Task<IActionResult> ShipOrder(
        string tenantId,
        Guid orderId,
        [FromBody] ShipShopeeOrderRequest request,
        CancellationToken cancellationToken)
    {
        Result<ShopeeOrderResponse> result = await sender.Send(
            new ShipShopeeOrderCommand(
                tenantId, orderId, request.Method, request.AddressId,
                request.PickupTimeId, request.BranchId),
            cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{tenantId}/orders/{orderId:guid}/relink")]
    public async Task<IActionResult> RelinkOrder(
        string tenantId, Guid orderId, CancellationToken cancellationToken)
    {
        Result<ShopeeOrderResponse> result = await sender.Send(
            new RelinkShopeeOrderItemsCommand(tenantId, orderId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{tenantId}/orders/{orderId:guid}/label")]
    public async Task<IActionResult> DownloadOrderLabel(
        string tenantId, Guid orderId, CancellationToken cancellationToken)
    {
        Result<ShopeeOrderLabelResult> result = await sender.Send(
            new GetShopeeOrderLabelQuery(tenantId, orderId), cancellationToken);
        if (result.IsFailure)
        {
            return ToActionResult(result);
        }

        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }
}
