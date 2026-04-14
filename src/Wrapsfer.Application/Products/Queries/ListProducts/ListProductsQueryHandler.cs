using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Common;
using Wrapsfer.Application.Products.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Products.Queries.ListProducts;

internal sealed class ListProductsQueryHandler(
    IProductRepository productRepository,
    ITenantContext tenantContext) : IQueryHandler<ListProductsQuery, PagedResult<ProductResponse>>
{
    public async Task<Result<PagedResult<ProductResponse>>> Handle(ListProductsQuery request,
        CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        (IReadOnlyList<Product> items, int totalCount) = await productRepository.ListAsync(
            tenantId,
            request.Search,
            request.Type,
            request.Page,
            request.PageSize,
            cancellationToken);

        List<Guid> bundleIds = items
            .Where(p => p.Type == ProductType.Bundle)
            .Select(p => p.Id)
            .ToList();

        IReadOnlyDictionary<Guid, IReadOnlyList<(int ChildStock, int Ratio)>> bundleComponentData =
            bundleIds.Count > 0
                ? await productRepository.GetBundleComponentDataAsync(bundleIds, tenantId, cancellationToken)
                : new Dictionary<Guid, IReadOnlyList<(int ChildStock, int Ratio)>>();

        List<ProductResponse> responses = items.Select(p =>
        {
            int? computedStock = null;

            if (p.Type == ProductType.Bundle)
            {
                computedStock = bundleComponentData.TryGetValue(p.Id,
                    out IReadOnlyList<(int ChildStock, int Ratio)>? data)
                    ? Product.ComputeBundleQuantity(data)
                    : 0;
            }

            return ProductResponse.FromProduct(p, computedStock);
        }).ToList();

        return new PagedResult<ProductResponse>(responses, totalCount, request.Page, request.PageSize);
    }
}
