using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Shopee.Common;
using Wrapsfer.Application.Shopee.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Shopee.Queries.ListShopeeProductLinks;

internal sealed class ListShopeeProductLinksQueryHandler(
    IShopeeShopConnectionRepository connectionRepository,
    IShopeeProductLinkRepository linkRepository,
    IProductRepository productRepository)
    : IQueryHandler<ListShopeeProductLinksQuery, IReadOnlyList<ShopeeProductLinkResponse>>
{
    public async Task<Result<IReadOnlyList<ShopeeProductLinkResponse>>> Handle(
        ListShopeeProductLinksQuery request,
        CancellationToken cancellationToken)
    {
        ShopeeShopConnection? connection =
            await connectionRepository.GetByTenantIdAsync(request.TenantId, cancellationToken);
        if (connection is null)
        {
            return Result<IReadOnlyList<ShopeeProductLinkResponse>>.Failure(
                ShopeeProductLinkErrors.ConnectionNotFound);
        }

        IReadOnlyList<ShopeeProductLink> links =
            await linkRepository.ListByTenantAsync(request.TenantId, cancellationToken);
        IReadOnlyList<Product> products = await productRepository.GetByIdsAsync(
            links.Select(l => l.ProductId), request.TenantId, cancellationToken);
        Dictionary<Guid, Product> productsById = products.ToDictionary(p => p.Id);

        List<ShopeeProductLinkResponse> responses = links
            .Where(l => productsById.ContainsKey(l.ProductId))
            .Select(l => ShopeeProductLinkResponseMapper.Map(l, productsById[l.ProductId]))
            .ToList();

        return Result<IReadOnlyList<ShopeeProductLinkResponse>>.Success(responses);
    }
}
