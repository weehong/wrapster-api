using Wrapster.Application.Abstractions.Messaging;
using Wrapster.Application.Products.Responses;

namespace Wrapster.Application.Products.Queries.GetProductBySku;

public sealed record GetProductBySkuQuery(string SkuCode) : IQuery<ProductResponse>;
