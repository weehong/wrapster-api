using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Domain.Repositories;

public interface IProductComponentRepository
{
    Task<ProductComponent?> GetByIdAsync(Guid id, string tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductComponent>> GetByParentIdAsync(Guid parentProductId, string tenantId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductComponent>> GetByChildIdAsync(Guid childProductId, string tenantId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductComponent>> GetByParentIdsAsync(IEnumerable<Guid> parentProductIds, string tenantId,
        CancellationToken cancellationToken = default);

    void Add(ProductComponent component);
    void Remove(ProductComponent component);
    Task RemoveAllByParentIdAsync(Guid parentProductId, string tenantId, CancellationToken cancellationToken = default);
}
