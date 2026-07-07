using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Products.Common;
using Wrapsfer.Application.Products.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Products.Queries.GetProductById;

internal sealed class GetProductByIdQueryHandler(
    IProductRepository productRepository,
    IProductComponentRepository productComponentRepository,
    ITenantContext tenantContext) : IQueryHandler<GetProductByIdQuery, ProductResponse>
{
    public async Task<Result<ProductResponse>> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;
        Product? product = await productRepository.GetByIdAsync(request.Id, tenantId, cancellationToken);
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
