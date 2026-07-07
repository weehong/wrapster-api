using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Api.Contracts;

public sealed record BulkUpdateWaybillStatusRequest(
    IReadOnlyList<Guid> Ids,
    WaybillStatus Status,
    string? Reason);
