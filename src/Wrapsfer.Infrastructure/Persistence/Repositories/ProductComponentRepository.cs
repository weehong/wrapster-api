using Microsoft.EntityFrameworkCore;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Infrastructure.Persistence.Repositories;

internal sealed class ProductComponentRepository(ApplicationDbContext context) : IProductComponentRepository
{
    public async Task<ProductComponent?> GetByIdAsync(Guid id, string tenantId,
        CancellationToken cancellationToken = default) =>
        await context.ProductComponents
            .FirstOrDefaultAsync(pc => pc.Id == id && pc.TenantId == tenantId, cancellationToken);

    public async Task<IReadOnlyList<ProductComponent>> GetByParentIdAsync(Guid parentProductId, string tenantId,
        CancellationToken cancellationToken = default) =>
        await context.ProductComponents
            .Where(pc => pc.ParentProductId == parentProductId && pc.TenantId == tenantId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProductComponent>> GetByChildIdAsync(Guid childProductId, string tenantId,
        CancellationToken cancellationToken = default) =>
        await context.ProductComponents
            .Where(pc => pc.ChildProductId == childProductId && pc.TenantId == tenantId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProductComponent>> GetByParentIdsAsync(IEnumerable<Guid> parentProductIds,
        string tenantId, CancellationToken cancellationToken = default)
    {
        List<Guid> ids = parentProductIds.ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await context.ProductComponents
            .Where(pc => pc.TenantId == tenantId && ids.Contains(pc.ParentProductId))
            .ToListAsync(cancellationToken);
    }

    public void Add(ProductComponent component) => context.ProductComponents.Add(component);

    public void Remove(ProductComponent component) => context.ProductComponents.Remove(component);

    public async Task RemoveAllByParentIdAsync(Guid parentProductId, string tenantId,
        CancellationToken cancellationToken = default) =>
        await context.ProductComponents
            .Where(pc => pc.ParentProductId == parentProductId && pc.TenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken);
}
