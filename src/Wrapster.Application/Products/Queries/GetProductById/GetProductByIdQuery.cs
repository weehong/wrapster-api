using Wrapster.Application.Abstractions.Messaging;
using Wrapster.Application.Products.Responses;

namespace Wrapster.Application.Products.Queries.GetProductById;

public sealed record GetProductByIdQuery(Guid Id) : IQuery<ProductResponse>;
