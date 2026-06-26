using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Common;
using Wrapsfer.Application.Products.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Products.Queries.ListProductAuditLogs;

internal sealed class ListProductAuditLogsQueryHandler(
    IProductRepository productRepository,
    IAuditLogRepository auditLogRepository,
    ITenantContext tenantContext) : IQueryHandler<ListProductAuditLogsQuery, PagedResult<ProductAuditLogResponse>>
{
    public async Task<Result<PagedResult<ProductAuditLogResponse>>> Handle(
        ListProductAuditLogsQuery request,
        CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;
        Product? product = await productRepository.GetByIdAsync(request.ProductId, tenantId, cancellationToken);
        if (product is null)
        {
            return Result<PagedResult<ProductAuditLogResponse>>.Failure(ProductErrors.NotFound);
        }

        (IReadOnlyList<AuditLog> items, int totalCount) = await auditLogRepository.ListForEntityAsync(
            nameof(Product),
            request.ProductId.ToString(),
            tenantId,
            request.Page,
            request.PageSize,
            cancellationToken);

        List<ProductAuditLogResponse> responses = items
            .Select(ProductAuditLogResponse.FromAuditLog)
            .ToList();

        return new PagedResult<ProductAuditLogResponse>(responses, totalCount, request.Page, request.PageSize);
    }
}
