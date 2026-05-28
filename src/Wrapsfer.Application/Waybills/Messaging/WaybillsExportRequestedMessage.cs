using Wrapsfer.Application.Waybills.Commands.RequestWaybillsExport;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Application.Waybills.Messaging;

public sealed record WaybillsExportRequestedMessage(
    Guid JobId,
    IReadOnlyList<string> TenantIds,
    string? RequestedBy,
    WaybillExportFormat Format,
    DateTime RequestedAt,
    DateOnly? From,
    DateOnly? To,
    WaybillStatus? Status,
    string? Search,
    IReadOnlyList<string>? RecipientEmails = null)
{
    public const string QueueName = "wrapsfer.report";
}
