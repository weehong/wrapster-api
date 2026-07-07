using MediatR;
using Microsoft.AspNetCore.Mvc;
using Wrapsfer.Application.Billing.Commands.CreateBillingPortalSession;
using Wrapsfer.Application.Billing.Commands.CreateStockReportCheckout;
using Wrapsfer.Application.Billing.Queries.GetStockReportBillingStatus;
using Wrapsfer.Application.Billing.Queries.GetStockReportDownloadCounts;
using Wrapsfer.Application.Billing.Queries.ListBillingHistory;
using Wrapsfer.Application.Billing.Responses;
using Wrapsfer.Domain.Common;

namespace Wrapsfer.Api.Controllers.V1;

[Route("api/v{version:apiVersion}/billing")]
public sealed class BillingController(ISender sender) : ApiControllerBase
{
    [HttpPost("stock-report/checkout")]
    public async Task<IActionResult> CreateStockReportCheckout(CancellationToken cancellationToken)
    {
        Result<CreateStockReportCheckoutResult> result =
            await sender.Send(new CreateStockReportCheckoutCommand(), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("stock-report/status")]
    public async Task<IActionResult> GetStockReportStatus(CancellationToken cancellationToken)
    {
        Result<StockReportBillingStatusResult> result =
            await sender.Send(new GetStockReportBillingStatusQuery(), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("portal-session")]
    public async Task<IActionResult> CreatePortalSession(CancellationToken cancellationToken)
    {
        Result<CreateBillingPortalSessionResult> result =
            await sender.Send(new CreateBillingPortalSessionCommand(), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("history")]
    public async Task<IActionResult> ListHistory(CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<BillingHistoryItemResponse>> result =
            await sender.Send(new ListBillingHistoryQuery(), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("stock-report/downloads")]
    public async Task<IActionResult> GetStockReportDownloads(
        [FromQuery] string? fromMonth,
        [FromQuery] string? toMonth,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<StockReportMonthlyDownloadCount>> result = await sender.Send(
            new GetStockReportDownloadCountsQuery(fromMonth ?? string.Empty, toMonth ?? string.Empty),
            cancellationToken);
        return ToActionResult(result);
    }
}
