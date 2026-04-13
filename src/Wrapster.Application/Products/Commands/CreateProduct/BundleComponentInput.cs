namespace Wrapster.Application.Products.Commands.CreateProduct;

public sealed record BundleComponentInput(Guid ChildProductId, int Quantity);
