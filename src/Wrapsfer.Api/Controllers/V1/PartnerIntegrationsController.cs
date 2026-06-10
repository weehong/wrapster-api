using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wrapsfer.Api.Contracts;
using Wrapsfer.Application.PartnerIntegrations.Commands.CreatePartnerIntegrationCredential;
using Wrapsfer.Application.PartnerIntegrations.Commands.DisablePartnerIntegrationCredential;
using Wrapsfer.Application.PartnerIntegrations.Commands.EnablePartnerIntegrationCredential;
using Wrapsfer.Application.PartnerIntegrations.Commands.RotatePartnerIntegrationCredential;
using Wrapsfer.Application.PartnerIntegrations.Queries.GetPartnerIntegrationCredential;
using Wrapsfer.Application.PartnerIntegrations.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Infrastructure.Authentication;

namespace Wrapsfer.Api.Controllers.V1;

[Authorize(Policy = AuthorizationPolicies.PartnerIntegrationAdmin)]
[Route("api/v{version:apiVersion}/partner-integrations")]
public sealed class PartnerIntegrationsController(ISender sender) : ApiControllerBase
{
    [HttpGet("{tenantId}/credentials")]
    public async Task<IActionResult> GetCredential(string tenantId, CancellationToken cancellationToken)
    {
        Result<PartnerIntegrationCredentialResponse> result =
            await sender.Send(new GetPartnerIntegrationCredentialQuery(tenantId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{tenantId}/credentials")]
    public async Task<IActionResult> CreateCredential(
        string tenantId,
        [FromBody] CreatePartnerIntegrationCredentialRequest request,
        CancellationToken cancellationToken)
    {
        Result<CreatePartnerIntegrationCredentialResponse> result = await sender.Send(
            new CreatePartnerIntegrationCredentialCommand(tenantId, request.DisplayName), cancellationToken);
        return ToCreatedResult(result);
    }

    [HttpPost("{tenantId}/credentials/rotate")]
    public async Task<IActionResult> RotateCredential(string tenantId, CancellationToken cancellationToken)
    {
        Result<RotatePartnerIntegrationCredentialResponse> result = await sender.Send(
            new RotatePartnerIntegrationCredentialCommand(tenantId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{tenantId}/credentials/disable")]
    public async Task<IActionResult> DisableCredential(string tenantId, CancellationToken cancellationToken)
    {
        Result<PartnerIntegrationCredentialResponse> result = await sender.Send(
            new DisablePartnerIntegrationCredentialCommand(tenantId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{tenantId}/credentials/enable")]
    public async Task<IActionResult> EnableCredential(string tenantId, CancellationToken cancellationToken)
    {
        Result<PartnerIntegrationCredentialResponse> result = await sender.Send(
            new EnablePartnerIntegrationCredentialCommand(tenantId), cancellationToken);
        return ToActionResult(result);
    }
}
