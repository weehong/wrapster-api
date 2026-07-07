using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Common;
using Wrapsfer.Application.StockAlerts.Responses;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Application.StockAlerts.Queries.ListStockAlerts;

public sealed record ListStockAlertsQuery(
    Guid? ProductId,
    StockAlertType? AlertType,
    DateTime? From,
    DateTime? To,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<StockAlertLogResponse>>;
