using Wrapster.Application.Abstractions.Messaging;
using Wrapster.Application.Common;
using Wrapster.Application.Products.Responses;
using Wrapster.Domain.Enums;

namespace Wrapster.Application.Products.Queries.ListProducts;

public sealed record ListProductsQuery(
    string? Search,
    ProductType? Type,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<ProductResponse>>;
