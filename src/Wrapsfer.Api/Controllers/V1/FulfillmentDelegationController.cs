using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Wrapsfer.Api.Contracts;
using Wrapsfer.Api.Filters;
using Wrapsfer.Application.FulfillmentDelegations.Commands.CancelFulfillmentDelegation;
using Wrapsfer.Application.FulfillmentDelegations.Commands.RequestFulfillmentDelegation;
using Wrapsfer.Application.FulfillmentDelegations.Commands.UpdateFulfillmentDelegationShipping;
using Wrapsfer.Application.FulfillmentDelegations.Queries.GetFulfillmentDelegation;
using Wrapsfer.Application.FulfillmentDelegations.Responses;
using Wrapsfer.Domain.Common;

namespace Wrapsfer.Api.Controllers.V1;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/fulfillment-delegation")]
[ServiceFilter(typeof(TenantResolutionFilter))]
public sealed class FulfillmentDelegationController(ISender sender) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        Result<FulfillmentDelegationResponse> result =
            await sender.Send(new GetFulfillmentDelegationQuery(), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("request")]
    public async Task<IActionResult> RequestDelegation([FromBody] RequestFulfillmentDelegationRequest request,
        CancellationToken cancellationToken)
    {
        Result<FulfillmentDelegationResponse> result = await sender.Send(
            new RequestFulfillmentDelegationCommand(request.DefaultShippingMethod), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel(CancellationToken cancellationToken)
    {
        Result result = await sender.Send(new CancelFulfillmentDelegationCommand(), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPatch("shipping")]
    public async Task<IActionResult> UpdateShipping(
        [FromBody] UpdateFulfillmentDelegationShippingRequest request,
        CancellationToken cancellationToken)
    {
        Result<FulfillmentDelegationResponse> result = await sender.Send(
            new UpdateFulfillmentDelegationShippingCommand(request.DefaultShippingMethod), cancellationToken);
        return ToActionResult(result);
    }
}
