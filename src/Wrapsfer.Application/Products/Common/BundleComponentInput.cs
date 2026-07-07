namespace Wrapsfer.Application.Products.Common;

public sealed record BundleComponentInput(Guid ChildProductId, int Quantity);
