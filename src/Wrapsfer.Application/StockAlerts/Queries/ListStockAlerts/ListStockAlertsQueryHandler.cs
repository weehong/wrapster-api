using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Common;
using Wrapsfer.Application.StockAlerts.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.StockAlerts.Queries.ListStockAlerts;

internal sealed class ListStockAlertsQueryHandler(
    IStockAlertLogRepository stockAlertLogRepository,
    ITenantContext tenantContext)
    : IQueryHandler<ListStockAlertsQuery, PagedResult<StockAlertLogResponse>>
{
    public async Task<Result<PagedResult<StockAlertLogResponse>>> Handle(ListStockAlertsQuery request,
        CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        (IReadOnlyList<StockAlertLog> items, int totalCount) = await stockAlertLogRepository.ListAsync(
            tenantId,
            request.ProductId,
            request.AlertType,
            request.From,
            request.To,
            request.Page,
            request.PageSize,
            cancellationToken);

        List<StockAlertLogResponse> responses = items
            .Select(StockAlertLogResponse.FromEntity)
            .ToList();

        return new PagedResult<StockAlertLogResponse>(responses, totalCount, request.Page, request.PageSize);
    }
}
