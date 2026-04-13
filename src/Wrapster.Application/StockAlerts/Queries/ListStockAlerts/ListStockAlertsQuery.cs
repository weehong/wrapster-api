using Wrapster.Application.Abstractions.Messaging;
using Wrapster.Application.Common;
using Wrapster.Application.StockAlerts.Responses;
using Wrapster.Domain.Enums;

namespace Wrapster.Application.StockAlerts.Queries.ListStockAlerts;

public sealed record ListStockAlertsQuery(
    Guid? ProductId,
    StockAlertType? AlertType,
    DateTime? From,
    DateTime? To,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<StockAlertLogResponse>>;
