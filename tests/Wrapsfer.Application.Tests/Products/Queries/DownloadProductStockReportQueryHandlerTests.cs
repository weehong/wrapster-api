using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.FileProcessing;
using Wrapsfer.Application.Products.Queries.DownloadProductStockReport;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Products.Queries;

public class DownloadProductStockReportQueryHandlerTests
{
    private readonly Mock<IStockMovementRepository> _stockMovementRepository = new();
    private readonly Mock<IProductStockReportFileWriter> _writer = new();
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly DownloadProductStockReportQueryHandler _handler;

    private IReadOnlyList<ProductStockReportRow>? _capturedRows;
    private ProductStockReportMetadata? _capturedMetadata;
    private DateTime _capturedAsOf;

    public DownloadProductStockReportQueryHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns("partner-a");
        _tenantContext.Setup(x => x.Username).Returns("acct");
        _tenantContext.Setup(x => x.DisplayName).Returns("Accountant");

        _stockMovementRepository
            .Setup(r => r.GetPointInTimeSnapshotAsync(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductStockSnapshot>
            {
                new(Guid.NewGuid(), "BC-1", "SKU-1", "Alpha", 4, 2.50m),
                new(Guid.NewGuid(), "BC-2", null, "Beta", 10, 1.00m)
            })
            .Callback<string, DateTime, CancellationToken>((_, asOf, _) => _capturedAsOf = asOf);

        _writer
            .Setup(w => w.WriteAsync(
                It.IsAny<IReadOnlyList<ProductStockReportRow>>(),
                It.IsAny<ProductStockReportMetadata>(),
                It.IsAny<ProductStockReportFormat>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 1, 2, 3 })
            .Callback<IReadOnlyList<ProductStockReportRow>, ProductStockReportMetadata,
                ProductStockReportFormat, CancellationToken>((rows, metadata, _, _) =>
            {
                _capturedRows = rows;
                _capturedMetadata = metadata;
            });

        _handler = new DownloadProductStockReportQueryHandler(
            _stockMovementRepository.Object, _writer.Object, _tenantContext.Object);
    }

    [Fact]
    public async Task Handle_MapsSnapshotsToRowsWithValuationAndTotals()
    {
        DownloadProductStockReportQuery query = new(ProductStockReportFormat.Xlsx, new DateOnly(2026, 6, 20));

        Result<DownloadProductStockReportResult> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().Equal(1, 2, 3);
        result.Value.ContentType.Should()
            .Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        result.Value.FileName.Should().Be("product-stock-report-20260620.xlsx");

        _capturedRows.Should().NotBeNull();
        _capturedRows!.Should().HaveCount(2);
        _capturedRows.Single(r => r.Barcode == "BC-1").TotalValue.Should().Be(10.00m);
        _capturedRows.Single(r => r.Barcode == "BC-2").TotalValue.Should().Be(10.00m);

        _capturedMetadata!.TotalProducts.Should().Be(2);
        _capturedMetadata.TotalUnits.Should().Be(14);
        _capturedMetadata.GrandTotalValue.Should().Be(20.00m);
        _capturedMetadata.ExportedBy.Should().Be("Accountant");
        _capturedMetadata.AsOfDate.Should().Be(new DateOnly(2026, 6, 20));
    }

    [Fact]
    public async Task Handle_QueriesRepositoryWithTenantAndEndOfDayCutoff()
    {
        DownloadProductStockReportQuery query = new(ProductStockReportFormat.Pdf, new DateOnly(2026, 6, 20));

        Result<DownloadProductStockReportResult> result = await _handler.Handle(query, CancellationToken.None);

        result.Value.ContentType.Should().Be("application/pdf");
        result.Value.FileName.Should().Be("product-stock-report-20260620.pdf");

        // The whole of 2026-06-20 is included: movements strictly before 2026-06-21 00:00 UTC.
        _capturedAsOf.Should().Be(new DateTime(2026, 6, 21, 0, 0, 0, DateTimeKind.Utc));
        _stockMovementRepository.Verify(
            r => r.GetPointInTimeSnapshotAsync("partner-a", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
