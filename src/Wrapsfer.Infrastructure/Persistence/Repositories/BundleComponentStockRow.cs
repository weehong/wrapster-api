namespace Wrapsfer.Infrastructure.Persistence.Repositories;

internal sealed record BundleComponentStockRow(Guid ParentProductId, int ChildStock, int Ratio);
