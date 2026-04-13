using Wrapster.Application.Abstractions.FileProcessing;

namespace Wrapster.Application.Products.Messaging;

public sealed record ProductsExportRequestedMessage(
    string TenantId,
    string? RequestedBy,
    ProductFileFormat Format,
    DateTime RequestedAt)
{
    public const string QueueName = "products.export";
}
