using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Tests.Helpers;
using Wrapsfer.Application.Waybills.Queries.GetStaleDraftsReport;
using Wrapsfer.Application.Waybills.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Waybills.Queries;

public class GetStaleDraftsReportQueryHandlerTests
{
    private const string TenantId = "test-tenant";
    private readonly GetStaleDraftsReportQueryHandler _handler;

    private readonly Mock<IWaybillRepository> _waybillRepository = new();
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<ITenantContext> _tenantContext = new();

    public GetStaleDraftsReportQueryHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns(TenantId);
        _handler = new GetStaleDraftsReportQueryHandler(
            _waybillRepository.Object,
            _productRepository.Object,
            _tenantContext.Object);
    }

    private static Waybill CreateWaybill(string number, Guid? productId = null, string? productBarcode = null)
    {
        Waybill waybill = Waybill.Create(TenantId, new DateOnly(2026, 5, 1), number).Value;

        if (productId.HasValue)
        {
            waybill.AddOrIncrementItem(productId.Value, productBarcode ?? "BC-1", 1);
        }

        return waybill;
    }

    [Fact]
    public async Task Handle_WhenNoDates_PassesNullBounds()
    {
        _waybillRepository.Setup(r => r.GetAutoCancelledBetweenAsync(
                TenantId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Waybill>());

        Result<IReadOnlyList<WaybillResponse>> result = await _handler.Handle(
            new GetStaleDraftsReportQuery(null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
        _waybillRepository.Verify(r => r.GetAutoCancelledBetweenAsync(
            TenantId, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithDateRange_ConvertsToUtcBoundsWithExclusiveUpper()
    {
        DateTime expectedFrom = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        // `to` is inclusive of the whole day, so the upper bound is the start of the next day.
        DateTime expectedToExclusive = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        _waybillRepository.Setup(r => r.GetAutoCancelledBetweenAsync(
                TenantId, expectedFrom, expectedToExclusive, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Waybill>());

        Result<IReadOnlyList<WaybillResponse>> result = await _handler.Handle(
            new GetStaleDraftsReportQuery(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _waybillRepository.Verify(r => r.GetAutoCancelledBetweenAsync(
            TenantId,
            It.Is<DateTime?>(d => d == expectedFrom && d.Value.Kind == DateTimeKind.Utc),
            It.Is<DateTime?>(d => d == expectedToExclusive && d.Value.Kind == DateTimeKind.Utc),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ResolvesProductNamesWithinTenant()
    {
        Product product = ProductTestFactory.CreateSingle(tenantId: TenantId, barcode: "BC-1", name: "Widget");
        Waybill waybill = CreateWaybill("WB-1", product.Id, "BC-1");

        _waybillRepository.Setup(r => r.GetAutoCancelledBetweenAsync(
                TenantId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Waybill> { waybill });
        _productRepository.Setup(r => r.GetByIdsAsync(
                It.Is<IEnumerable<Guid>>(ids => ids.Contains(product.Id)), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });

        Result<IReadOnlyList<WaybillResponse>> result = await _handler.Handle(
            new GetStaleDraftsReportQuery(null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].Items[0].ProductName.Should().Be("Widget");
    }
}
