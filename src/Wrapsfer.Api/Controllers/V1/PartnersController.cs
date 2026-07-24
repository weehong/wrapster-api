using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wrapsfer.Api.Contracts;
using Wrapsfer.Application.Billing.Commands.RefundStockReportAccess;
using Wrapsfer.Application.Billing.Queries.GetPartnersBillingOverview;
using Wrapsfer.Application.Billing.Queries.GetPartnerStockReportBillingStatus;
using Wrapsfer.Application.Billing.Queries.GetPartnerStockReportDownloadCounts;
using Wrapsfer.Application.Billing.Queries.GetStockReportBillingStatus;
using Wrapsfer.Application.Billing.Responses;
using Wrapsfer.Application.FulfillmentDelegations.Commands.AcceptFulfillmentDelegation;
using Wrapsfer.Application.FulfillmentDelegations.Commands.DeclineFulfillmentDelegation;
using Wrapsfer.Application.FulfillmentDelegations.Commands.RevokeFulfillmentDelegation;
using Wrapsfer.Application.FulfillmentDelegations.Queries.GetFulfillmentDelegationByTenantId;
using Wrapsfer.Application.FulfillmentDelegations.Queries.ListFulfillmentDelegations;
using Wrapsfer.Application.FulfillmentDelegations.Responses;
using Wrapsfer.Application.Partners.Commands.CreatePartner;
using Wrapsfer.Application.Partners.Commands.RetryPartnerProvisioning;
using Wrapsfer.Application.Partners.Commands.SetPartnerActive;
using Wrapsfer.Application.Partners.Queries.GetPartnerByTenantId;
using Wrapsfer.Application.Partners.Queries.ListPartners;
using Wrapsfer.Application.Partners.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Enums;
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
    public async Task<IActionResult> List(
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<PartnerResponse>> result =
            await sender.Send(new ListPartnersQuery(isActive), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("fulfillment-delegations")]
    public async Task<IActionResult> ListFulfillmentDelegations(
        [FromQuery] FulfillmentDelegationStatus? status,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<FulfillmentDelegationResponse>> result =
            await sender.Send(new ListFulfillmentDelegationsQuery(status), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{tenantId}/fulfillment-delegation")]
    public async Task<IActionResult> GetFulfillmentDelegation(string tenantId,
        CancellationToken cancellationToken)
    {
        Result<FulfillmentDelegationResponse> result =
            await sender.Send(new GetFulfillmentDelegationByTenantIdQuery(tenantId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{tenantId}/fulfillment-delegation/accept")]
    public async Task<IActionResult> AcceptFulfillmentDelegation(string tenantId,
        CancellationToken cancellationToken)
    {
        Result<FulfillmentDelegationResponse> result =
            await sender.Send(new AcceptFulfillmentDelegationCommand(tenantId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{tenantId}/fulfillment-delegation/decline")]
    public async Task<IActionResult> DeclineFulfillmentDelegation(
        string tenantId,
        [FromBody] DeclineFulfillmentDelegationRequest request,
        CancellationToken cancellationToken)
    {
        Result<FulfillmentDelegationResponse> result = await sender.Send(
            new DeclineFulfillmentDelegationCommand(tenantId, request.Reason), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{tenantId}/fulfillment-delegation/revoke")]
    public async Task<IActionResult> RevokeFulfillmentDelegation(
        string tenantId,
        [FromBody] RevokeFulfillmentDelegationRequest request,
        CancellationToken cancellationToken)
    {
        Result<FulfillmentDelegationResponse> result = await sender.Send(
            new RevokeFulfillmentDelegationCommand(tenantId, request.Reason), cancellationToken);
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
            request.DisplayName,
            request.AdminEmail,
            request.AdminUsername,
            request.TemporaryPassword,
            request.IsTemporaryPassword,
            request.ContactEmail);

        Result<PartnerResponse> result = await sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("billing/overview")]
    public async Task<IActionResult> GetBillingOverview(CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<PartnerBillingOverviewItem>> result =
            await sender.Send(new GetPartnersBillingOverviewQuery(), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{tenantId}/billing/stock-report/status")]
    public async Task<IActionResult> GetStockReportBillingStatus(
        string tenantId,
        CancellationToken cancellationToken)
    {
        Result<StockReportBillingStatusResult> result =
            await sender.Send(new GetPartnerStockReportBillingStatusQuery(tenantId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{tenantId}/billing/stock-report/refund")]
    public async Task<IActionResult> RefundStockReportAccess(
        string tenantId,
        CancellationToken cancellationToken)
    {
        Result result = await sender.Send(
            new RefundStockReportAccessCommand(tenantId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{tenantId}/billing/stock-report/downloads")]
    public async Task<IActionResult> GetStockReportDownloads(
        string tenantId,
        [FromQuery] string? fromMonth,
        [FromQuery] string? toMonth,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<StockReportMonthlyDownloadCount>> result = await sender.Send(
            new GetPartnerStockReportDownloadCountsQuery(
                tenantId, fromMonth ?? string.Empty, toMonth ?? string.Empty),
            cancellationToken);
        return ToActionResult(result);
    }
}
