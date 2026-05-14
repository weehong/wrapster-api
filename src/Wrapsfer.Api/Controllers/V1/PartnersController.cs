using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wrapsfer.Api.Contracts;
using Wrapsfer.Application.Partners.Commands.CreatePartner;
using Wrapsfer.Application.Partners.Commands.RetryPartnerProvisioning;
using Wrapsfer.Application.Partners.Commands.SetPartnerActive;
using Wrapsfer.Application.Partners.Queries.GetPartnerByTenantId;
using Wrapsfer.Application.Partners.Queries.ListPartners;
using Wrapsfer.Application.Partners.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Infrastructure.Authentication;

namespace Wrapsfer.Api.Controllers.V1;

[Authorize(Policy = AuthorizationPolicies.OwnerAdminOnly)]
public sealed class PartnersController(ISender sender) : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePartnerRequest request,
        CancellationToken cancellationToken)
    {
        CreatePartnerCommand command = new(
            request.TenantId,
            request.DisplayName,
            request.AdminEmail,
            request.AdminUsername,
            request.TemporaryPassword,
            request.IsTemporaryPassword,
            request.ContactEmail);

        Result<PartnerResponse> result = await sender.Send(command, cancellationToken);
        return ToCreatedResult(result);
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<PartnerResponse>> result =
            await sender.Send(new ListPartnersQuery(), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{tenantId}")]
    public async Task<IActionResult> GetByTenantId(string tenantId, CancellationToken cancellationToken)
    {
        Result<PartnerResponse> result =
            await sender.Send(new GetPartnerByTenantIdQuery(tenantId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPatch("{tenantId}/active")]
    public async Task<IActionResult> SetActive(
        string tenantId,
        [FromBody] SetPartnerActiveRequest request,
        CancellationToken cancellationToken)
    {
        Result<PartnerResponse> result =
            await sender.Send(new SetPartnerActiveCommand(tenantId, request.IsActive), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{tenantId}/retry")]
    public async Task<IActionResult> RetryProvisioning(
        string tenantId,
        [FromBody] RetryPartnerProvisioningRequest request,
        CancellationToken cancellationToken)
    {
        RetryPartnerProvisioningCommand command = new(
            tenantId,
            request.AdminEmail,
            request.AdminUsername,
            request.TemporaryPassword,
            request.IsTemporaryPassword);

        Result<PartnerResponse> result = await sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }
}
