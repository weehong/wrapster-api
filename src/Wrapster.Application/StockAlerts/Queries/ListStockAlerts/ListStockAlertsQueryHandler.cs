using Wrapster.Application.Abstractions;
using Wrapster.Application.Abstractions.Messaging;
using Wrapster.Application.Common;
using Wrapster.Application.StockAlerts.Responses;
using Wrapster.Domain.Common;
using Wrapster.Domain.Entities;
using Wrapster.Domain.Repositories;

namespace Wrapster.Application.StockAlerts.Queries.ListStockAlerts;

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
