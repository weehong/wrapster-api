using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wrapsfer.Api.Contracts;
using Wrapsfer.Api.Filters;
using Wrapsfer.Application.Common;
using Wrapsfer.Application.PurchaseOrders.Commands.CreatePurchaseOrder;
using Wrapsfer.Application.PurchaseOrders.Commands.ReceivePurchaseOrder;
using Wrapsfer.Application.PurchaseOrders.Commands.RejectPurchaseOrder;
using Wrapsfer.Application.PurchaseOrders.Queries.GetPurchaseOrderById;
using Wrapsfer.Application.PurchaseOrders.Queries.ListPurchaseOrders;
using Wrapsfer.Application.PurchaseOrders.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Infrastructure.Authentication;

namespace Wrapsfer.Api.Controllers.V1;

[Route("api/v{version:apiVersion}/purchase-orders")]
[ServiceFilter(typeof(TenantResolutionFilter))]
public sealed class PurchaseOrdersController(ISender sender) : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePurchaseOrderRequest request,
        CancellationToken cancellationToken)
    {
        Result<Guid> result = await sender.Send(
            new CreatePurchaseOrderCommand(request.PoNumber, request.ProductId, request.Quantity),
            cancellationToken);
        return ToCreatedResult(result);
    }

    [HttpGet]
    [AllowOwnerTenantScope]
    public async Task<IActionResult> List(
        [FromQuery] string? tenantId,
        [FromQuery] PurchaseOrderStatus? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        bool includeAllPartnerTenants =
            HttpContext.Items[TenantResolutionFilter.OwnerCrossTenantScopeKey] is true;

        Result<PagedResult<PurchaseOrderResponse>> result = await sender.Send(
            new ListPurchaseOrdersQuery(status, search, page, pageSize, includeAllPartnerTenants),
            cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        Result<PurchaseOrderResponse> result =
            await sender.Send(new GetPurchaseOrderByIdQuery(id), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{id:guid}/receive")]
    [Authorize(Policy = AuthorizationPolicies.OwnerAdminOnly)]
    public async Task<IActionResult> Receive(Guid id, CancellationToken cancellationToken)
    {
        Result result = await sender.Send(new ReceivePurchaseOrderCommand(id), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = AuthorizationPolicies.OwnerAdminOnly)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectPurchaseOrderRequest request,
        CancellationToken cancellationToken)
    {
        Result result = await sender.Send(new RejectPurchaseOrderCommand(id, request.Reason), cancellationToken);
        return ToActionResult(result);
    }
}
