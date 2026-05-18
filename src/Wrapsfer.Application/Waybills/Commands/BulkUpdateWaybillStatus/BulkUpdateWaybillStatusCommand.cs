using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Waybills.Responses;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Application.Waybills.Commands.BulkUpdateWaybillStatus;

public sealed record BulkUpdateWaybillStatusCommand(
    IReadOnlyList<Guid> Ids,
    WaybillStatus Status,
    string? Reason) : ICommand<BulkWaybillStatusUpdateResult>;
