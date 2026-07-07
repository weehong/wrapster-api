using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Tests.Helpers;
using Wrapsfer.Application.Waybills.Commands.CreateWaybill;
using Wrapsfer.Application.Waybills.Services;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Waybills.Commands;

public class CreateWaybillCommandHandlerTests
{
    private const string TenantId = "test-tenant";
    private readonly Mock<IProductComponentRepository> _componentRepository = new();
    private readonly CreateWaybillCommandHandler _handler;
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private readonly Mock<IWaybillRepository> _waybillRepository = new();

    public CreateWaybillCommandHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns(TenantId);
        StockReservationService stockService =
            new(_productRepository.Object, _componentRepository.Object);
        _handler = new CreateWaybillCommandHandler(_waybillRepository.Object, _productRepository.Object,
            stockService, _tenantContext.Object, _unitOfWork.Object);
    }

    [Fact]
    public async Task Handle_WhenNumberAlreadyExists_ReturnsDuplicate()
    {
        _waybillRepository.Setup(r =>
                r.ExistsByNumberInAnyTenantAsync("WB-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        CreateWaybillCommand command = new(
            new DateOnly(2026, 4, 13),
            "WB-1",
            new List<CreateWaybillItem> { new("BC-1", 1) });

        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(WaybillErrors.DuplicateWaybillNumber.Code);
    }

    [Fact]
    public async Task Handle_WhenNumberExistsUnderAnotherTenant_ReturnsDuplicate()
    {
        // The repository check spans all tenants: a number registered under any other
        // tenant must block creation here too.
        _waybillRepository.Setup(r =>
                r.ExistsByNumberInAnyTenantAsync("WB-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        CreateWaybillCommand command = new(
            new DateOnly(2026, 4, 13),
            "WB-1",
            new List<CreateWaybillItem> { new("BC-1", 1) });

        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(WaybillErrors.DuplicateWaybillNumber.Code);
        _waybillRepository.Verify(r => r.Add(It.IsAny<Waybill>()), Times.Never);
    }

    [Fact]
    public async Task Handle_PassesTrimmedNumberToDuplicateCheck()
    {
        Product product = ProductTestFactory.CreateSingle(barcode: "BC-1", stockQuantity: 10);

        _waybillRepository.Setup(r =>
                r.ExistsByNumberInAnyTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _productRepository.Setup(r =>
                r.GetByBarcodesAsync(It.IsAny<IEnumerable<string>>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        _productRepository.Setup(r =>
                r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });

        CreateWaybillCommand command = new(
            new DateOnly(2026, 4, 13),
            " WB-1 ",
            new List<CreateWaybillItem> { new("BC-1", 1) });

        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _waybillRepository.Verify(r =>
            r.ExistsByNumberInAnyTenantAsync("WB-1", It.IsAny<CancellationToken>()), Times.Once);
        _waybillRepository.Verify(r => r.Add(It.Is<Waybill>(w => w.WaybillNumber == "WB-1")), Times.Once);
    }

    [Fact]
    public async Task Handle_Success_AddsWaybillAndSaves()
    {
        Product product = ProductTestFactory.CreateSingle(barcode: "BC-1", stockQuantity: 10);

        _waybillRepository.Setup(r =>
                r.ExistsByNumberInAnyTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _productRepository.Setup(r =>
                r.GetByBarcodesAsync(It.IsAny<IEnumerable<string>>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        _productRepository.Setup(r =>
                r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });

        CreateWaybillCommand command = new(
            new DateOnly(2026, 4, 13),
            "WB-1",
            new List<CreateWaybillItem> { new("BC-1", 3) });

        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        product.ReservedQuantity.Should().Be(3);
        _waybillRepository.Verify(r => r.Add(It.Is<Waybill>(w =>
            w.Items.Count == 1 && w.Items.First().ProductId == product.Id)), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenBarcodeNotFound_ReturnsProductNotFound()
    {
        _waybillRepository.Setup(r =>
                r.ExistsByNumberInAnyTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _productRepository.Setup(r =>
                r.GetByBarcodesAsync(It.IsAny<IEnumerable<string>>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product>());

        CreateWaybillCommand command = new(
            new DateOnly(2026, 4, 13),
            "WB-1",
            new List<CreateWaybillItem> { new("MISSING-BC", 1) });

        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.NotFound.Code);
        _waybillRepository.Verify(r => r.Add(It.IsAny<Waybill>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenProductInactive_ReturnsInactiveAndDoesNotSave()
    {
        Product product = ProductTestFactory.CreateSingle(barcode: "BC-1", stockQuantity: 10);
        product.Deactivate(DateTime.UtcNow);

        _waybillRepository.Setup(r =>
                r.ExistsByNumberInAnyTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _productRepository.Setup(r =>
                r.GetByBarcodesAsync(It.IsAny<IEnumerable<string>>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });

        CreateWaybillCommand command = new(
            new DateOnly(2026, 4, 13),
            "WB-1",
            new List<CreateWaybillItem> { new("BC-1", 2) });

        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.Inactive.Code);
        _waybillRepository.Verify(r => r.Add(It.IsAny<Waybill>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenReservationFails_ReturnsErrorAndDoesNotSave()
    {
        Product product = ProductTestFactory.CreateSingle(barcode: "BC-1", stockQuantity: 1);

        _waybillRepository.Setup(r =>
                r.ExistsByNumberInAnyTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _productRepository.Setup(r =>
                r.GetByBarcodesAsync(It.IsAny<IEnumerable<string>>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        _productRepository.Setup(r =>
                r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });

        CreateWaybillCommand command = new(
            new DateOnly(2026, 4, 13),
            "WB-1",
            new List<CreateWaybillItem> { new("BC-1", 5) });

        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.InsufficientStock.Code);
        _waybillRepository.Verify(r => r.Add(It.IsAny<Waybill>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
