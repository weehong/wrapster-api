using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Application.Abstractions.FileProcessing;

public sealed record WaybillReportRow(
    string TenantId,
    DateOnly PackagingDate,
    string WaybillNumber,
    WaybillStatus Status,
    string? CancellationReason,
    DateTime? PackedAt,
    DateTime? HandedOffAt,
    DateTime? CancelledAt,
    DateTime CreatedAt,
    string? CreatedBy,
    string? ProductBarcode,
    string? ProductName,
    int? Quantity);
