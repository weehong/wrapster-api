using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Common;
using Wrapsfer.Application.Products.Responses;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Application.Products.Queries.ListProducts;

public sealed record ListProductsQuery(
    string? Search,
    ProductType? Type,
    int Page = 1,
    int PageSize = 20,
    bool IncludeAllPartnerTenants = false) : IQuery<PagedResult<ProductResponse>>;
