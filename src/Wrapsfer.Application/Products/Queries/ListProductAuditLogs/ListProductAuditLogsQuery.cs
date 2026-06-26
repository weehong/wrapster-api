using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Common;
using Wrapsfer.Application.Products.Responses;

namespace Wrapsfer.Application.Products.Queries.ListProductAuditLogs;

public sealed record ListProductAuditLogsQuery(
    Guid ProductId,
    int Page = 1,
    int PageSize = 50) : IQuery<PagedResult<ProductAuditLogResponse>>;
