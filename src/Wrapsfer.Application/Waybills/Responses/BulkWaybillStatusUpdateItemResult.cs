using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Application.Waybills.Responses;

public sealed record BulkWaybillStatusUpdateItemResult(
    Guid Id,
    bool Success,
    WaybillStatus? Status,
    string? ErrorCode,
    string? ErrorMessage);
