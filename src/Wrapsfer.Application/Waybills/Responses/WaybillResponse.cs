using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Application.Waybills.Responses;

public sealed record WaybillResponse(
    Guid Id,
    string TenantId,
    DateOnly PackagingDate,
    string WaybillNumber,
    WaybillStatus Status,
    string? CancellationReason,
    DateTime? PackedAt,
    DateTime? HandedOffAt,
    DateTime? CancelledAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? CreatedBy,
    IReadOnlyList<WaybillItemResponse> Items,
    Guid? ShopeeOrderId);
