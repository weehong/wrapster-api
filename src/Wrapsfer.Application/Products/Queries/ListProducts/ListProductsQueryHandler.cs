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
    IPartnerTenantRepository partnerTenantRepository,
    ITenantContext tenantContext) : IQueryHandler<ListProductsQuery, PagedResult<ProductResponse>>
{
    public async Task<Result<PagedResult<ProductResponse>>> Handle(ListProductsQuery request,
        CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        IReadOnlyList<string> tenantIds;
        IReadOnlyList<Product> items;
        int totalCount;

        if (request.IncludeAllPartnerTenants)
        {
            IReadOnlyList<PartnerTenant> partnerTenants = await partnerTenantRepository.ListAsync(cancellationToken);
            tenantIds = partnerTenants.Select(p => p.TenantId).ToList();

            (items, totalCount) = await productRepository.ListByTenantIdsAsync(
                tenantIds,
                request.Search,
                request.Type,
                request.IncludeInactive,
                request.Page,
                request.PageSize,
                cancellationToken);
        }
        else
        {
            tenantIds = [tenantId];
            (items, totalCount) = await productRepository.ListAsync(
                tenantId,
                request.Search,
                request.Type,
                request.IncludeInactive,
                request.Page,
                request.PageSize,
                cancellationToken);
        }

        List<Guid> bundleIds = items
            .Where(p => p.Type == ProductType.Bundle)
            .Select(p => p.Id)
            .ToList();

        IReadOnlyDictionary<Guid, IReadOnlyList<(int ChildStock, int Ratio)>> bundleComponentData =
            bundleIds.Count > 0
                ? request.IncludeAllPartnerTenants
                    ? await productRepository.GetBundleComponentDataByTenantIdsAsync(bundleIds, tenantIds, cancellationToken)
                    : await productRepository.GetBundleComponentDataAsync(bundleIds, tenantId, cancellationToken)
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
