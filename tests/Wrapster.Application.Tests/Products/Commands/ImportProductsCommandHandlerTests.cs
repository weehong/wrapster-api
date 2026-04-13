using Microsoft.Extensions.Logging.Abstractions;
using Wrapster.Application.Abstractions;
using Wrapster.Application.Abstractions.FileProcessing;
using Wrapster.Application.Products;
using Wrapster.Application.Products.Commands.ImportProducts;
using Wrapster.Application.Products.Responses;
using Wrapster.Application.Tests.Helpers;
using Wrapster.Domain.Abstractions;
using Wrapster.Domain.Common;
using Wrapster.Domain.Entities;
using Wrapster.Domain.Enums;
using Wrapster.Domain.Repositories;

namespace Wrapster.Application.Tests.Products.Commands;

public class ImportProductsCommandHandlerTests
{
    private const string TenantId = "test-tenant";

    private readonly Mock<IProductFileParser> _parser = new();
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IProductComponentRepository> _componentRepository = new();
    private readonly Mock<ITenantSettingsRepository> _tenantSettingsRepository = new();
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly ImportProductsCommandHandler _handler;

    public ImportProductsCommandHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns(TenantId);
        _tenantSettingsRepository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Wrapster.Domain.Entities.TenantSettings?)null);

        IOptions<ProductSettings> settings = Options.Create(new ProductSettings { GlobalLowStockThreshold = 10 });
        _handler = new ImportProductsCommandHandler(
            _parser.Object,
            _productRepository.Object,
            _componentRepository.Object,
            _tenantSettingsRepository.Object,
            _tenantContext.Object,
            _unitOfWork.Object,
            settings,
            NullLogger<ImportProductsCommandHandler>.Instance);
    }

    private void SetupParse(IReadOnlyList<ProductImportRow> rows, IReadOnlyList<RowError>? parseErrors = null)
    {
        _parser.Setup(p =>
                p.ParseAsync(It.IsAny<Stream>(), It.IsAny<ProductFileFormat>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductImportBatch(rows, parseErrors ?? []));
    }

    private void SetupExistingByBarcodes(params Product[] existing)
    {
        _productRepository.Setup(r => r.GetByBarcodesAsync(It.IsAny<IEnumerable<string>>(), TenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
    }

    private static ProductImportRow Row(int n, string barcode, string name, string type, string cost = "9.99",
        string stock = "10", string? components = null, string? unpackTarget = null, string? unpackQty = null)
        => new(n, barcode, name, null, type, cost, stock, null, unpackTarget, unpackQty, components);

    [Fact]
    public async Task Handle_WhenEmptyFile_ReturnsEmptyFileError()
    {
        SetupParse(rows: []);

        Result<ProductImportResult> result =
            await _handler.Handle(new ImportProductsCommand(Stream.Null, ProductFileFormat.Csv),
                CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ProductImport.EmptyFile");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenRowInvalid_ReturnsErrorsAndSkipsSaveChanges()
    {
        SetupParse([Row(2, "BC1", "Widget", "NotAType")]);
        SetupExistingByBarcodes();

        Result<ProductImportResult> result =
            await _handler.Handle(new ImportProductsCommand(Stream.Null, ProductFileFormat.Csv),
                CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CreatedCount.Should().Be(0);
        result.Value.Errors.Should().ContainSingle(e => e.Column == "type");
        _productRepository.Verify(r => r.Add(It.IsAny<Product>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDuplicateBarcodeInFile_ReportsDuplicateError()
    {
        SetupParse([
            Row(2, "BC1", "Widget", "Single"),
            Row(3, "BC1", "Widget Copy", "Single")
        ]);
        SetupExistingByBarcodes();

        Result<ProductImportResult> result =
            await _handler.Handle(new ImportProductsCommand(Stream.Null, ProductFileFormat.Csv),
                CancellationToken.None);

        result.Value.Errors.Should().Contain(e =>
            e.RowNumber == 3 && e.Message.Contains("also appears on row 2"));
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenBundleChildNotFound_ReportsError()
    {
        SetupParse([Row(2, "BC-BUNDLE", "Kit", "Bundle", components: "UNKNOWN:2")]);
        SetupExistingByBarcodes();

        Result<ProductImportResult> result =
            await _handler.Handle(new ImportProductsCommand(Stream.Null, ProductFileFormat.Csv),
                CancellationToken.None);

        result.Value.Errors.Should().Contain(e =>
            e.Column == "components" && e.Message.Contains("doesn't exist"));
    }

    [Fact]
    public async Task Handle_WhenAllValid_CreatesProductsAndSavesOnce()
    {
        SetupParse([Row(2, "BC1", "Widget", "Single")]);
        SetupExistingByBarcodes();

        Result<ProductImportResult> result =
            await _handler.Handle(new ImportProductsCommand(Stream.Null, ProductFileFormat.Csv),
                CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CreatedCount.Should().Be(1);
        result.Value.UpdatedCount.Should().Be(0);
        result.Value.Errors.Should().BeEmpty();
        _productRepository.Verify(r => r.Add(It.IsAny<Product>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenBarcodeExists_UpdatesInsteadOfCreating()
    {
        Product existing = ProductTestFactory.CreateSingle(barcode: "BC1", name: "Old Name");
        SetupParse([Row(2, "BC1", "New Name", "Single", cost: "19.99", stock: "50")]);
        SetupExistingByBarcodes(existing);

        Result<ProductImportResult> result =
            await _handler.Handle(new ImportProductsCommand(Stream.Null, ProductFileFormat.Csv),
                CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UpdatedCount.Should().Be(1);
        result.Value.CreatedCount.Should().Be(0);
        existing.Name.Should().Be("New Name");
        existing.Cost.Should().Be(19.99m);
        _productRepository.Verify(r => r.Add(It.IsAny<Product>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
