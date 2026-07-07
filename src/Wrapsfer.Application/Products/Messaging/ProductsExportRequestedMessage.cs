using Wrapsfer.Application.Abstractions.FileProcessing;

namespace Wrapsfer.Application.Products.Messaging;

public sealed record ProductsExportRequestedMessage(
    string TenantId,
    string? RequestedBy,
    ProductFileFormat Format,
    DateTime RequestedAt)
{
    public const string QueueName = "wrapsfer.products";
}
