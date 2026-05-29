using MediatR;
using Microsoft.AspNetCore.Mvc;
using Wrapsfer.Api.Contracts;
using Wrapsfer.Api.Filters;
using Wrapsfer.Application.Common;
using Wrapsfer.Application.Waybills.Commands.AddWaybillItem;
using Wrapsfer.Application.Waybills.Commands.BulkUpdateWaybillStatus;
using Wrapsfer.Application.Waybills.Commands.CancelWaybill;
using Wrapsfer.Application.Waybills.Commands.CreateWaybill;
using Wrapsfer.Application.Waybills.Commands.DeleteWaybill;
using Wrapsfer.Application.Waybills.Commands.DeleteWaybillsExportJob;
using Wrapsfer.Application.Waybills.Commands.EmailWaybillsReport;
using Wrapsfer.Application.Waybills.Commands.MarkWaybillHandedOff;
using Wrapsfer.Application.Waybills.Commands.MarkWaybillPacked;
using Wrapsfer.Application.Waybills.Commands.RemoveWaybillItem;
using Wrapsfer.Application.Waybills.Commands.RequestWaybillsExport;
using Wrapsfer.Application.Waybills.Commands.RetryWaybillsExport;
using Wrapsfer.Application.Waybills.Commands.UpdateWaybill;
using Wrapsfer.Application.Waybills.Commands.UpdateWaybillItemQuantity;
using Wrapsfer.Application.Waybills.Commands.UpdateWaybillNumber;
using Wrapsfer.Application.Waybills.Queries.CheckWaybillNumberAvailable;
using Wrapsfer.Application.Waybills.Queries.DownloadWaybillsExportFile;
using Wrapsfer.Application.Waybills.Queries.GetStaleDraftsReport;
using Wrapsfer.Application.Waybills.Queries.GetWaybillById;
using Wrapsfer.Application.Waybills.Queries.GetWaybillsByDate;
using Wrapsfer.Application.Waybills.Queries.ListWaybillExportJobs;
using Wrapsfer.Application.Waybills.Queries.ListWaybills;
using Wrapsfer.Application.Waybills.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Api.Controllers.V1;

[ServiceFilter(typeof(TenantResolutionFilter))]
public sealed class WaybillsController(ISender sender) : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWaybillRequest request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CreateWaybillItem> items = request.Items is null
            ? Array.Empty<CreateWaybillItem>()
            : request.Items.Select(i => new CreateWaybillItem(i.Barcode, i.Quantity)).ToList();

        Result<Guid> result = await sender.Send(
            new CreateWaybillCommand(request.PackagingDate, request.WaybillNumber, items), cancellationToken);
        return ToCreatedResult(result);
    }

    [HttpGet]
    [AllowOwnerTenantScope]
    public async Task<IActionResult> List(
        [FromQuery] string? tenantId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] WaybillStatus? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        bool includeAllPartnerTenants =
            HttpContext.Items[TenantResolutionFilter.OwnerCrossTenantScopeKey] is true;

        Result<PagedResult<WaybillResponse>> result = await sender.Send(
            new ListWaybillsQuery(from, to, status, search, page, pageSize, includeAllPartnerTenants),
            cancellationToken);
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
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWaybillRequest request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<UpdateWaybillItem> items = request.Items is null
            ? Array.Empty<UpdateWaybillItem>()
            : request.Items
                .Select(i => new UpdateWaybillItem(i.ProductId, i.Barcode, i.Quantity))
                .ToList();

        Result result = await sender.Send(
            new UpdateWaybillCommand(id, request.PackagingDate, request.WaybillNumber, items), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPatch("{id:guid}/waybill-number")]
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

    [HttpPost("bulk/status")]
    public async Task<IActionResult> BulkUpdateStatus(
        [FromBody] BulkUpdateWaybillStatusRequest request,
        CancellationToken cancellationToken)
    {
        BulkUpdateWaybillStatusCommand command = new(request.Ids, request.Status, request.Reason);
        Result<BulkWaybillStatusUpdateResult> result = await sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("export/jobs")]
    [AllowOwnerTenantScope]
    public async Task<IActionResult> ListExportJobs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        Result<PagedResult<WaybillExportJobResponse>> result = await sender.Send(
            new ListWaybillExportJobsQuery(page, pageSize),
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpPost("export")]
    [AllowOwnerTenantScope]
    public async Task<IActionResult> Export(
        [FromQuery] string? tenantId,
        [FromQuery] WaybillExportFormat format,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] WaybillStatus? status,
        [FromQuery] string? search,
        [FromQuery] string[]? partnerTenantIds,
        CancellationToken cancellationToken)
    {
        bool includeAllPartnerTenants =
            HttpContext.Items[TenantResolutionFilter.OwnerCrossTenantScopeKey] is true;

        Result result = await sender.Send(
            new RequestWaybillsExportCommand(
                format,
                from,
                to,
                status,
                search,
                includeAllPartnerTenants,
                partnerTenantIds),
            cancellationToken);

        return result.IsSuccess
            ? Accepted(new { message = "Export requested.", format })
            : ToActionResult(result);
    }

    [HttpPost("export/jobs/{id:guid}/retry")]
    [AllowOwnerTenantScope]
    public async Task<IActionResult> RetryExportJob(Guid id, CancellationToken cancellationToken)
    {
        Result result = await sender.Send(new RetryWaybillsExportCommand(id), cancellationToken);

        return result.IsSuccess
            ? Accepted(new { message = "Export retry requested.", jobId = id })
            : ToActionResult(result);
    }

    [HttpPost("export/email")]
    [AllowOwnerTenantScope]
    public async Task<IActionResult> ExportEmail(
        [FromQuery] string? tenantId,
        [FromQuery] WaybillExportFormat format,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] WaybillStatus? status,
        [FromQuery] string? search,
        [FromQuery] string[]? partnerTenantIds,
        [FromBody] EmailWaybillsReportRequest request,
        CancellationToken cancellationToken)
    {
        bool includeAllPartnerTenants =
            HttpContext.Items[TenantResolutionFilter.OwnerCrossTenantScopeKey] is true;

        IReadOnlyList<string> recipients = request?.RecipientEmails ?? Array.Empty<string>();

        Result<EmailWaybillsReportResult> result = await sender.Send(
            new EmailWaybillsReportCommand(
                format,
                recipients,
                from,
                to,
                status,
                search,
                includeAllPartnerTenants,
                partnerTenantIds),
            cancellationToken);

        return result.IsSuccess
            ? Accepted(new
            {
                message = "Report email queued.",
                deliveryMode = result.Value.DeliveryMode.ToString(),
                rowCount = result.Value.RowCount,
                format
            })
            : ToActionResult(result);
    }

    [HttpDelete("export/jobs/{id:guid}")]
    [AllowOwnerTenantScope]
    public async Task<IActionResult> DeleteExportJob(Guid id, CancellationToken cancellationToken)
    {
        Result result = await sender.Send(new DeleteWaybillsExportJobCommand(id), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("export/file")]
    [AllowOwnerTenantScope]
    public async Task<IActionResult> DownloadExportFile(
        [FromQuery] WaybillExportFormat format,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] WaybillStatus? status,
        [FromQuery] string? search,
        [FromQuery] string[]? partnerTenantIds,
        CancellationToken cancellationToken)
    {
        bool includeAllPartnerTenants =
            HttpContext.Items[TenantResolutionFilter.OwnerCrossTenantScopeKey] is true;

        Result<DownloadWaybillsExportFileResult> result = await sender.Send(
            new DownloadWaybillsExportFileQuery(
                format, from, to, status, search, includeAllPartnerTenants, partnerTenantIds),
            cancellationToken);

        return result.IsSuccess
            ? File(result.Value.Content, result.Value.ContentType, result.Value.FileName)
            : ToActionResult(result);
    }
}
