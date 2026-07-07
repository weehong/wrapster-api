using System.Globalization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Wrapsfer.Api.Contracts;
using Wrapsfer.Application.Shopee.Commands.CompleteShopeeAuthorization;
using Wrapsfer.Application.Shopee.Commands.DisconnectShopeeShop;
using Wrapsfer.Application.Shopee.Queries.GetShopeeAuthorizationLink;
using Wrapsfer.Application.Shopee.Queries.GetShopeeConnection;
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
}
