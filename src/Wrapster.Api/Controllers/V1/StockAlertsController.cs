using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Wrapster.Api.Filters;
using Wrapster.Application.Common;
using Wrapster.Application.StockAlerts.Queries.ListStockAlerts;
using Wrapster.Application.StockAlerts.Responses;
using Wrapster.Domain.Common;
using Wrapster.Domain.Enums;

namespace Wrapster.Api.Controllers.V1;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/stock-alerts")]
[ServiceFilter(typeof(TenantResolutionFilter))]
public sealed class StockAlertsController(ISender sender) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? productId,
        [FromQuery] StockAlertType? alertType,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        Result<PagedResult<StockAlertLogResponse>> result = await sender.Send(
            new ListStockAlertsQuery(productId, alertType, from, to, page, pageSize),
            cancellationToken);
        return ToActionResult(result);
    }
}
