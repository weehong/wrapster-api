using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Wrapster.Api.Contracts;
using Wrapster.Api.Filters;
using Wrapster.Application.TenantSettings.Commands.AddRecipient;
using Wrapster.Application.TenantSettings.Commands.RemoveRecipient;
using Wrapster.Application.TenantSettings.Commands.SetRecipientActive;
using Wrapster.Application.TenantSettings.Commands.UpsertTenantSettings;
using Wrapster.Application.TenantSettings.Queries.GetTenantSettings;
using Wrapster.Application.TenantSettings.Responses;
using Wrapster.Domain.Common;

namespace Wrapster.Api.Controllers.V1;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tenant-settings")]
[ServiceFilter(typeof(TenantResolutionFilter))]
public sealed class TenantSettingsController(ISender sender) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        Result<TenantSettingsResponse> result = await sender.Send(new GetTenantSettingsQuery(), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut]
    public async Task<IActionResult> Upsert([FromBody] UpsertTenantSettingsRequest request,
        CancellationToken cancellationToken)
    {
        UpsertTenantSettingsCommand command = new(request.DefaultLowStockThreshold,
            request.ClearDefaultLowStockThreshold);
        Result result = await sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("recipients")]
    public async Task<IActionResult> AddRecipient([FromBody] AddRecipientRequest request,
        CancellationToken cancellationToken)
    {
        Result<Guid> result = await sender.Send(new AddRecipientCommand(request.Email), cancellationToken);
        return ToCreatedResult(result);
    }

    [HttpDelete("recipients/{id:guid}")]
    public async Task<IActionResult> RemoveRecipient(Guid id, CancellationToken cancellationToken)
    {
        Result result = await sender.Send(new RemoveRecipientCommand(id), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPatch("recipients/{id:guid}")]
    public async Task<IActionResult> SetRecipientActive(Guid id, [FromBody] SetRecipientActiveRequest request,
        CancellationToken cancellationToken)
    {
        Result result = await sender.Send(new SetRecipientActiveCommand(id, request.IsActive), cancellationToken);
        return ToActionResult(result);
    }
}
