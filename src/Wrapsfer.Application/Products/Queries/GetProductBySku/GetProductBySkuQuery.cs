using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Products.Responses;

namespace Wrapsfer.Application.Products.Queries.GetProductBySku;

public sealed record GetProductBySkuQuery(string SkuCode) : IQuery<ProductResponse>;
