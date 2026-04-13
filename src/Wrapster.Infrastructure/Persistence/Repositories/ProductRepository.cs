using Microsoft.EntityFrameworkCore;
using Wrapster.Domain.Entities;
using Wrapster.Domain.Enums;
using Wrapster.Domain.Repositories;

namespace Wrapster.Infrastructure.Persistence.Repositories;

internal sealed class ProductRepository(ApplicationDbContext context) : IProductRepository
{
    public async Task<Product?> GetByIdAsync(Guid id, string tenantId, CancellationToken cancellationToken = default) =>
        await context.Products
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId, cancellationToken);

    public async Task<Product?> GetByBarcodeAsync(string barcode, string tenantId,
        CancellationToken cancellationToken = default) =>
        await context.Products
            .FirstOrDefaultAsync(p => p.Barcode == barcode && p.TenantId == tenantId, cancellationToken);

    public async Task<Product?> GetBySkuCodeAsync(string skuCode, string tenantId,
        CancellationToken cancellationToken = default) =>
        await context.Products
            .FirstOrDefaultAsync(p => p.SkuCode == skuCode && p.TenantId == tenantId, cancellationToken);

    public async Task<IReadOnlyList<Product>> GetByIdsAsync(IEnumerable<Guid> ids, string tenantId,
        CancellationToken cancellationToken = default)
    {
        List<Guid> idList = ids.ToList();
        return await context.Products
            .Where(p => p.TenantId == tenantId && idList.Contains(p.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetByBarcodesAsync(IEnumerable<string> barcodes, string tenantId,
        CancellationToken cancellationToken = default)
    {
        List<string> barcodeList = barcodes.ToList();
        return await context.Products
            .Where(p => p.TenantId == tenantId && barcodeList.Contains(p.Barcode))
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Product> Items, int TotalCount)> ListAsync(
        string tenantId,
        string? search = null,
        ProductType? type = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Product> query = context.Products
            .Where(p => p.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            string searchPattern = $"%{search}%";
            query = query.Where(p =>
                EF.Functions.ILike(p.Name, searchPattern) ||
                EF.Functions.ILike(p.Barcode, searchPattern) ||
                (p.SkuCode != null && EF.Functions.ILike(p.SkuCode, searchPattern)));
        }

        if (type.HasValue)
        {
            query = query.Where(p => p.Type == type.Value);
        }

        int totalCount = await query.CountAsync(cancellationToken);

        List<Product> items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<(int ChildStock, int Ratio)>>>
        GetBundleComponentDataAsync(IEnumerable<Guid> bundleIds, string tenantId,
            CancellationToken cancellationToken = default)
    {
        List<Guid> ids = bundleIds.ToList();

        var rows = await context.ProductComponents
            .Where(pc => ids.Contains(pc.ParentProductId) && pc.TenantId == tenantId)
            .Select(pc => new
            {
                pc.ParentProductId,
                ChildStock = pc.Child.StockQuantity,
                Ratio = pc.Quantity
            })
            .ToListAsync(cancellationToken);

        Dictionary<Guid, IReadOnlyList<(int ChildStock, int Ratio)>> result = rows
            .GroupBy(r => r.ParentProductId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<(int ChildStock, int Ratio)>)g
                    .Select(r => (r.ChildStock, r.Ratio))
                    .ToList());

        return result;
    }

    public async Task<bool> IsReferencedAsUnpackTargetAsync(Guid productId, string tenantId,
        CancellationToken cancellationToken = default) =>
        await context.Products
            .AnyAsync(p => p.UnpackTargetProductId == productId && p.TenantId == tenantId, cancellationToken);

    public void Add(Product product) => context.Products.Add(product);

    public void Remove(Product product) => context.Products.Remove(product);
}
