using Wrapsfer.Application.Abstractions.FileProcessing;

namespace Wrapsfer.Application.Waybills.Messaging;

public sealed record WaybillsExportRequestedMessage(
    string TenantId,
    string? RequestedBy,
    ProductFileFormat Format,
    DateTime RequestedAt)
{
    public const string QueueName = "waybills.export";
}
