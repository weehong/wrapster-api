using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Application.Waybills.Responses;

public sealed record WaybillExportJobResponse(
    Guid Id,
    string Format,
    WaybillExportJobStatus Status,
    IReadOnlyList<string> PartnerTenantIds,
    DateOnly? From,
    DateOnly? To,
    DateTime RequestedAt,
    DateTime? CompletedAt,
    string? FailureReason);
