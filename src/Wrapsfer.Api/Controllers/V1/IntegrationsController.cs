using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wrapsfer.Api.Contracts;
using Wrapsfer.Application.Waybills.Commands.CreateWaybill;
using Wrapsfer.Domain.Common;
using Wrapsfer.Infrastructure.Authentication;

namespace Wrapsfer.Api.Controllers.V1;

/// <summary>
/// Machine-to-machine surface for partner backend systems. This is the only controller
/// integration clients can call; every other endpoint rejects integration tokens.
/// Waybill items must reference existing products by barcode — unknown barcodes are
/// rejected, never created.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.IntegrationApiOnly)]
public sealed class IntegrationsController(ISender sender) : ApiControllerBase
{
    [HttpPost("waybills")]
    public async Task<IActionResult> CreateWaybill(
        [FromBody] CreateIntegrationWaybillRequest request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CreateWaybillItem> items = request.Items is null
            ? Array.Empty<CreateWaybillItem>()
            : request.Items.Select(i => new CreateWaybillItem(i.Barcode, i.Quantity)).ToList();

        Result<Guid> result = await sender.Send(
            new CreateWaybillCommand(request.PackagingDate, request.WaybillNumber, items), cancellationToken);
        return ToCreatedResult(result);
    }
}
