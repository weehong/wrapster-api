using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Domain.Repositories;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, string tenantId, CancellationToken cancellationToken = default);
    Task<Product?> GetByBarcodeAsync(string barcode, string tenantId, CancellationToken cancellationToken = default);
    Task<Product?> GetBySkuCodeAsync(string skuCode, string tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> GetByIdsAsync(IEnumerable<Guid> ids, string tenantId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> GetByIdsByTenantIdsAsync(IEnumerable<Guid> ids,
        IReadOnlyCollection<string> tenantIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> GetByBarcodesAsync(IEnumerable<string> barcodes, string tenantId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Product> Items, int TotalCount)> ListAsync(string tenantId, string? search = null,
        ProductType? type = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Product> Items, int TotalCount)> ListByTenantIdsAsync(IReadOnlyCollection<string> tenantIds,
        string? search = null, ProductType? type = null, int page = 1, int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, IReadOnlyList<(int ChildStock, int Ratio)>>> GetBundleComponentDataAsync(
        IEnumerable<Guid> bundleIds, string tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, IReadOnlyList<(int ChildStock, int Ratio)>>> GetBundleComponentDataByTenantIdsAsync(
        IEnumerable<Guid> bundleIds, IReadOnlyCollection<string> tenantIds,
        CancellationToken cancellationToken = default);

    Task<bool> IsReferencedAsUnpackTargetAsync(Guid productId, string tenantId,
        CancellationToken cancellationToken = default);

    void Add(Product product);
    void Remove(Product product);
}
