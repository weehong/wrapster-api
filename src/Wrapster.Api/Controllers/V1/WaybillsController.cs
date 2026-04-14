using MediatR;
using Microsoft.AspNetCore.Mvc;
using Wrapster.Api.Contracts;
using Wrapster.Api.Filters;
using Wrapster.Application.Abstractions.FileProcessing;
using Wrapster.Application.Common;
using Wrapster.Application.Waybills.Commands.AddWaybillItem;
using Wrapster.Application.Waybills.Commands.CancelWaybill;
using Wrapster.Application.Waybills.Commands.CreateWaybill;
using Wrapster.Application.Waybills.Commands.DeleteWaybill;
using Wrapster.Application.Waybills.Commands.MarkWaybillHandedOff;
using Wrapster.Application.Waybills.Commands.MarkWaybillPacked;
using Wrapster.Application.Waybills.Commands.RemoveWaybillItem;
using Wrapster.Application.Waybills.Commands.RequestWaybillsExport;
using Wrapster.Application.Waybills.Commands.UpdateWaybillItemQuantity;
using Wrapster.Application.Waybills.Commands.UpdateWaybillNumber;
using Wrapster.Application.Waybills.Queries.CheckWaybillNumberAvailable;
using Wrapster.Application.Waybills.Queries.GetStaleDraftsReport;
using Wrapster.Application.Waybills.Queries.GetWaybillById;
using Wrapster.Application.Waybills.Queries.GetWaybillsByDate;
using Wrapster.Application.Waybills.Queries.ListWaybills;
using Wrapster.Application.Waybills.Responses;
using Wrapster.Domain.Common;
using Wrapster.Domain.Enums;

namespace Wrapster.Api.Controllers.V1;

[ServiceFilter(typeof(TenantResolutionFilter))]
public sealed class WaybillsController(ISender sender) : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWaybillRequest request,
        CancellationToken cancellationToken)
    {
        Result<Guid> result = await sender.Send(
            new CreateWaybillCommand(request.PackagingDate, request.WaybillNumber), cancellationToken);
        return ToCreatedResult(result);
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] WaybillStatus? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        Result<PagedResult<WaybillResponse>> result = await sender.Send(
            new ListWaybillsQuery(from, to, status, search, page, pageSize), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        Result<WaybillResponse> result = await sender.Send(new GetWaybillByIdQuery(id), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("by-date/{packagingDate}")]
    public async Task<IActionResult> GetByDate(DateOnly packagingDate, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<WaybillResponse>> result =
            await sender.Send(new GetWaybillsByDateQuery(packagingDate), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("number-available")]
    public async Task<IActionResult> CheckNumberAvailable([FromQuery] string waybillNumber,
        CancellationToken cancellationToken)
    {
        Result<bool> result =
            await sender.Send(new CheckWaybillNumberAvailableQuery(waybillNumber), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("stale-drafts-report")]
    public async Task<IActionResult> GetStaleDraftsReport([FromQuery] int hoursBack = 48,
        CancellationToken cancellationToken = default)
    {
        Result<IReadOnlyList<WaybillResponse>> result =
            await sender.Send(new GetStaleDraftsReportQuery(hoursBack), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateNumber(Guid id, [FromBody] UpdateWaybillNumberRequest request,
        CancellationToken cancellationToken)
    {
        Result result = await sender.Send(
            new UpdateWaybillNumberCommand(id, request.WaybillNumber), cancellationToken);
        return ToActionResult(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        Result result = await sender.Send(new DeleteWaybillCommand(id), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{id:guid}/items")]
    public async Task<IActionResult> AddItem(Guid id, [FromBody] AddWaybillItemRequest request,
        CancellationToken cancellationToken)
    {
        Result<Guid> result = await sender.Send(
            new AddWaybillItemCommand(id, request.Barcode, request.Quantity), cancellationToken);
        return ToCreatedResult(result);
    }

    [HttpPut("{id:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> UpdateItemQuantity(Guid id, Guid itemId,
        [FromBody] UpdateWaybillItemQuantityRequest request, CancellationToken cancellationToken)
    {
        Result result = await sender.Send(
            new UpdateWaybillItemQuantityCommand(id, itemId, request.Quantity), cancellationToken);
        return ToActionResult(result);
    }

    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> RemoveItem(Guid id, Guid itemId, CancellationToken cancellationToken)
    {
        Result result = await sender.Send(new RemoveWaybillItemCommand(id, itemId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{id:guid}/mark-packed")]
    public async Task<IActionResult> MarkPacked(Guid id, CancellationToken cancellationToken)
    {
        Result result = await sender.Send(new MarkWaybillPackedCommand(id), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{id:guid}/mark-handed-off")]
    public async Task<IActionResult> MarkHandedOff(Guid id, CancellationToken cancellationToken)
    {
        Result result = await sender.Send(new MarkWaybillHandedOffCommand(id), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelWaybillRequest request,
        CancellationToken cancellationToken)
    {
        Result result = await sender.Send(new CancelWaybillCommand(id, request.Reason), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("export")]
    public async Task<IActionResult> Export([FromQuery] ProductFileFormat format,
        CancellationToken cancellationToken)
    {
        Result result = await sender.Send(new RequestWaybillsExportCommand(format), cancellationToken);
        return result.IsSuccess
            ? Accepted(new { message = "Export requested.", format })
            : ToActionResult(result);
    }
}
