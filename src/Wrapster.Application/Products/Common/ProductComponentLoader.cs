using Wrapster.Application.Products.Responses;
using Wrapster.Domain.Common;
using Wrapster.Domain.Entities;
using Wrapster.Domain.Errors;
using Wrapster.Domain.Repositories;

namespace Wrapster.Application.Products.Common;

internal static class ProductComponentLoader
{
    public static async Task<Result<IReadOnlyList<ProductComponentResponse>>> LoadAsync(
        Guid parentProductId,
        string tenantId,
        IProductRepository productRepository,
        IProductComponentRepository productComponentRepository,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ProductComponent> components =
            await productComponentRepository.GetByParentIdAsync(parentProductId, tenantId, cancellationToken);

        if (components.Count == 0)
        {
            return Result<IReadOnlyList<ProductComponentResponse>>.Success([]);
        }

        List<Guid> childIds = components.Select(c => c.ChildProductId).ToList();
        IReadOnlyList<Product> children =
            await productRepository.GetByIdsAsync(childIds, tenantId, cancellationToken);
        Dictionary<Guid, Product> childMap = children.ToDictionary(c => c.Id);

        List<ProductComponentResponse> responses = new(components.Count);
        foreach (ProductComponent component in components)
        {
            if (!childMap.TryGetValue(component.ChildProductId, out Product? child))
            {
                return Result<IReadOnlyList<ProductComponentResponse>>.Failure(ProductErrors.ComponentNotFound);
            }

            responses.Add(new ProductComponentResponse(
                component.Id,
                child.Id,
                child.Name,
                child.Barcode,
                component.Quantity));
        }

        return Result<IReadOnlyList<ProductComponentResponse>>.Success(responses);
    }
}
