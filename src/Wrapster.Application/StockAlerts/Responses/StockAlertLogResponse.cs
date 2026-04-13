using Wrapster.Domain.Entities;
using Wrapster.Domain.Enums;

namespace Wrapster.Application.StockAlerts.Responses;

public sealed record StockAlertLogResponse(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string Barcode,
    StockAlertType AlertType,
    int StockQuantity,
    int Threshold,
    StockAlertDeliveryStatus DeliveryStatus,
    string? RecipientsNotified,
    string? FailureReason,
    DateTime OccurredOn)
{
    public static StockAlertLogResponse FromEntity(StockAlertLog log) => new(
        log.Id,
        log.ProductId,
        log.ProductName,
        log.Barcode,
        log.AlertType,
        log.StockQuantity,
        log.Threshold,
        log.DeliveryStatus,
        log.RecipientsNotified,
        log.FailureReason,
        log.OccurredOn);
}
