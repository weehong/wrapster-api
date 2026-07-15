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
using Wrapsfer.Application.Shopee.Commands.LinkShopeeProduct;
using Wrapsfer.Application.Shopee.Commands.SyncShopeeProductStock;
using Wrapsfer.Application.Shopee.Commands.UnlinkShopeeProduct;
using Wrapsfer.Application.Shopee.Queries.GetShopeeAuthorizationLink;
using Wrapsfer.Application.Shopee.Queries.GetShopeeConnection;
using Wrapsfer.Application.Shopee.Queries.GetShopeeShopItems;
using Wrapsfer.Application.Shopee.Queries.ListShopeeProductLinks;
using Wrapsfer.Application.Shopee.Responses;
using Wrapsfer.Domain.Common;
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
}
