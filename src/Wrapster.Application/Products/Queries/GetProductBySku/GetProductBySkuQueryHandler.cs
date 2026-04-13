using Wrapster.Application.Abstractions;
using Wrapster.Application.Abstractions.Messaging;
using Wrapster.Application.Products.Common;
using Wrapster.Application.Products.Responses;
using Wrapster.Domain.Common;
using Wrapster.Domain.Entities;
using Wrapster.Domain.Enums;
using Wrapster.Domain.Errors;
using Wrapster.Domain.Repositories;

namespace Wrapster.Application.Products.Queries.GetProductBySku;

internal sealed class GetProductBySkuQueryHandler(
    IProductRepository productRepository,
    IProductComponentRepository productComponentRepository,
    ITenantContext tenantContext) : IQueryHandler<GetProductBySkuQuery, ProductResponse>
{
    public async Task<Result<ProductResponse>> Handle(GetProductBySkuQuery request, CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;
        Product? product =
            await productRepository.GetBySkuCodeAsync(request.SkuCode, tenantId, cancellationToken);
        if (product is null)
        {
            return Result<ProductResponse>.Failure(ProductErrors.NotFound);
        }

        Result<IReadOnlyList<ProductComponentResponse>> componentsResult = await ProductComponentLoader.LoadAsync(
            product.Id, tenantId, productRepository, productComponentRepository, cancellationToken);
        if (!componentsResult.IsSuccess)
        {
            return Result<ProductResponse>.Failure(componentsResult.Error);
        }

        int? computedStock = null;

        if (product.Type == ProductType.Bundle)
        {
            IReadOnlyDictionary<Guid, IReadOnlyList<(int ChildStock, int Ratio)>> componentData =
                await productRepository.GetBundleComponentDataAsync([product.Id], tenantId, cancellationToken);

            computedStock = componentData.TryGetValue(product.Id, out IReadOnlyList<(int ChildStock, int Ratio)>? data)
                ? Product.ComputeBundleQuantity(data)
                : 0;
        }

        return ProductResponse.FromProduct(product, computedStock, componentsResult.Value);
    }
}
