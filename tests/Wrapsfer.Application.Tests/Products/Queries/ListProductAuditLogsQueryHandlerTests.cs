using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Common;
using Wrapsfer.Application.Products.Queries.ListProductAuditLogs;
using Wrapsfer.Application.Products.Responses;
using Wrapsfer.Application.Tests.Helpers;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Products.Queries;

public class ListProductAuditLogsQueryHandlerTests
{
    private const string TenantId = "test-tenant";
    private readonly ListProductAuditLogsQueryHandler _handler;
    private readonly Mock<IAuditLogRepository> _auditLogRepository = new();
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<ITenantContext> _tenantContext = new();

    public ListProductAuditLogsQueryHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns(TenantId);
        _handler = new ListProductAuditLogsQueryHandler(
            _productRepository.Object,
            _auditLogRepository.Object,
            _tenantContext.Object);
    }

    [Fact]
    public async Task Handle_WhenProductNotFound_ReturnsNotFoundFailure()
    {
        Guid productId = Guid.NewGuid();
        _productRepository.Setup(r => r.GetByIdAsync(productId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        Result<PagedResult<ProductAuditLogResponse>> result = await _handler.Handle(
            new ListProductAuditLogsQuery(productId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.NotFound.Code);
        _auditLogRepository.Verify(r => r.ListForEntityAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenProductExists_ReturnsTenantScopedAuditLogs()
    {
        Product product = ProductTestFactory.CreateSingle(tenantId: TenantId);
        List<AuditLog> auditLogs =
        [
            new()
            {
                EntityName = nameof(Product),
                EntityId = product.Id.ToString(),
                TenantId = TenantId,
                Action = AuditAction.Updated,
                Changes = "{\"StockQuantity\":{\"OldValue\":859,\"NewValue\":8561}}",
                UserId = "user-1",
                Username = "owneradmin",
                Timestamp = new DateTime(2026, 6, 25, 9, 0, 22, DateTimeKind.Utc)
            }
        ];

        _productRepository.Setup(r => r.GetByIdAsync(product.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _auditLogRepository.Setup(r => r.ListForEntityAsync(
                nameof(Product),
                product.Id.ToString(),
                TenantId,
                1,
                50,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((auditLogs as IReadOnlyList<AuditLog>, 1));

        Result<PagedResult<ProductAuditLogResponse>> result = await _handler.Handle(
            new ListProductAuditLogsQuery(product.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Changes.Should().Contain("StockQuantity");
        result.Value.TotalCount.Should().Be(1);
        _auditLogRepository.Verify(r => r.ListForEntityAsync(
            nameof(Product),
            product.Id.ToString(),
            TenantId,
            1,
            50,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
