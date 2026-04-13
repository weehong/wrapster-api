using Wrapster.Domain.Common;
using Wrapster.Domain.Enums;

namespace Wrapster.Domain.Entities;

public sealed class StockAlertLog : BaseEntity
{
    private StockAlertLog() { }

    public string TenantId { get; private set; } = default!;
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = default!;
    public string Barcode { get; private set; } = default!;
    public StockAlertType AlertType { get; private set; }
    public int StockQuantity { get; private set; }
    public int Threshold { get; private set; }
    public string? RecipientsNotified { get; private set; }
    public StockAlertDeliveryStatus DeliveryStatus { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime OccurredOn { get; private set; }

    public static StockAlertLog Create(
        string tenantId,
        Guid productId,
        string productName,
        string barcode,
        StockAlertType alertType,
        int stockQuantity,
        int threshold,
        StockAlertDeliveryStatus deliveryStatus,
        string? recipientsNotified,
        string? failureReason,
        DateTime occurredOn) =>
        new()
        {
            TenantId = tenantId,
            ProductId = productId,
            ProductName = productName,
            Barcode = barcode,
            AlertType = alertType,
            StockQuantity = stockQuantity,
            Threshold = threshold,
            DeliveryStatus = deliveryStatus,
            RecipientsNotified = recipientsNotified,
            FailureReason = failureReason,
            OccurredOn = occurredOn
        };
}
