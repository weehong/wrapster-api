using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Products.Responses;

namespace Wrapsfer.Application.Products.Queries.GetProductById;

public sealed record GetProductByIdQuery(Guid Id) : IQuery<ProductResponse>;
