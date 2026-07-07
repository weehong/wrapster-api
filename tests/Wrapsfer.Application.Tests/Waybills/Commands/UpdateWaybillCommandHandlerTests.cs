using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Tests.Helpers;
using Wrapsfer.Application.Waybills.Commands.UpdateWaybill;
using Wrapsfer.Application.Waybills.Services;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Waybills.Commands;

public class UpdateWaybillCommandHandlerTests
{
    private const string TenantId = "test-tenant";
    private static readonly DateOnly PackagingDate = new(2026, 5, 21);
    private readonly Mock<IProductComponentRepository> _componentRepository = new();
    private readonly UpdateWaybillCommandHandler _handler;
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IWaybillRepository> _waybillRepository = new();

    public UpdateWaybillCommandHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns(TenantId);
        StockReservationService stockService =
            new(_productRepository.Object, _componentRepository.Object);
        _handler = new UpdateWaybillCommandHandler(
            _waybillRepository.Object,
            _productRepository.Object,
            stockService,
            _tenantContext.Object,
            _unitOfWork.Object);
    }

    [Fact]
    public async Task Handle_WhenWaybillNotFound_ReturnsNotFound()
    {
        _waybillRepository.Setup(r =>
                r.GetByIdWithItemsAsync(It.IsAny<Guid>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Waybill?)null);

        UpdateWaybillCommand command = new(Guid.NewGuid(), PackagingDate, "WB-1",
            new List<UpdateWaybillItem> { new(Guid.NewGuid(), null, 1) });

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(WaybillErrors.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenWaybillNumberConflicts_ReturnsDuplicate()
    {
        Waybill waybill = Waybill.Create(TenantId, PackagingDate, "WB-1").Value;
        _waybillRepository.Setup(r =>
                r.GetByIdWithItemsAsync(waybill.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(waybill);
        _waybillRepository.Setup(r =>
                r.ExistsByNumberInAnyTenantAsync("WB-2", waybill.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        UpdateWaybillCommand command = new(waybill.Id, PackagingDate, "WB-2",
            new List<UpdateWaybillItem>());

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(WaybillErrors.DuplicateWaybillNumber.Code);
    }

    [Fact]
    public async Task Handle_WhenNonDraftWaybill_FailsWithCannotEditNonDraft()
    {
        Waybill waybill = Waybill.Create(TenantId, PackagingDate, "WB-1").Value;
        Product product = ProductTestFactory.CreateSingle(stockQuantity: 10);
        waybill.AddOrIncrementItem(product.Id, product.Barcode, 2);
        waybill.MarkPacked();

        _waybillRepository.Setup(r =>
                r.GetByIdWithItemsAsync(waybill.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(waybill);
        _waybillRepository.Setup(r =>
                r.ExistsByNumberInAnyTenantAsync(It.IsAny<string>(), waybill.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _productRepository.Setup(r =>
                r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });

        UpdateWaybillCommand command = new(waybill.Id, PackagingDate, "WB-1",
            new List<UpdateWaybillItem> { new(product.Id, null, 5) });

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(WaybillErrors.CannotEditNonDraft.Code);
    }

    [Fact]
    public async Task Handle_AddsNewItemAndReservesStock()
    {
        Waybill waybill = Waybill.Create(TenantId, PackagingDate, "WB-1").Value;
        Product product = ProductTestFactory.CreateSingle(stockQuantity: 10);

        SetupRepos(waybill, [product]);

        UpdateWaybillCommand command = new(waybill.Id, PackagingDate, "WB-1",
            new List<UpdateWaybillItem> { new(product.Id, null, 4) });

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        product.ReservedQuantity.Should().Be(4);
        waybill.Items.Should().ContainSingle()
            .Which.Quantity.Should().Be(4);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RemovedItem_ReleasesReservedStock()
    {
        Product product = ProductTestFactory.CreateSingle(stockQuantity: 10);
        product.Reserve(3);
        Waybill waybill = Waybill.Create(TenantId, PackagingDate, "WB-1").Value;
        waybill.AddOrIncrementItem(product.Id, product.Barcode, 3);

        SetupRepos(waybill, [product]);

        UpdateWaybillCommand command = new(waybill.Id, PackagingDate, "WB-1",
            new List<UpdateWaybillItem>());

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        product.ReservedQuantity.Should().Be(0);
        waybill.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_DecreasedQuantity_ReleasesDelta()
    {
        Product product = ProductTestFactory.CreateSingle(stockQuantity: 10);
        product.Reserve(5);
        Waybill waybill = Waybill.Create(TenantId, PackagingDate, "WB-1").Value;
        waybill.AddOrIncrementItem(product.Id, product.Barcode, 5);

        SetupRepos(waybill, [product]);

        UpdateWaybillCommand command = new(waybill.Id, PackagingDate, "WB-1",
            new List<UpdateWaybillItem> { new(product.Id, null, 2) });

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        product.ReservedQuantity.Should().Be(2);
        waybill.Items.Should().ContainSingle()
            .Which.Quantity.Should().Be(2);
    }

    [Fact]
    public async Task Handle_IsIdempotent_NoChangesNoReservationCalls()
    {
        Product product = ProductTestFactory.CreateSingle(stockQuantity: 10);
        product.Reserve(3);
        Waybill waybill = Waybill.Create(TenantId, PackagingDate, "WB-1").Value;
        waybill.AddOrIncrementItem(product.Id, product.Barcode, 3);
        int initialReserved = product.ReservedQuantity;

        SetupRepos(waybill, [product]);

        UpdateWaybillCommand command = new(waybill.Id, PackagingDate, "WB-1",
            new List<UpdateWaybillItem> { new(product.Id, null, 3) });

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        product.ReservedQuantity.Should().Be(initialReserved);
        waybill.Items.Should().ContainSingle()
            .Which.Quantity.Should().Be(3);
    }

    [Fact]
    public async Task Handle_WhenProductInactive_ReturnsInactive()
    {
        Waybill waybill = Waybill.Create(TenantId, PackagingDate, "WB-1").Value;
        Product product = ProductTestFactory.CreateSingle(stockQuantity: 10);
        product.Deactivate(DateTime.UtcNow);

        SetupRepos(waybill, [product]);

        UpdateWaybillCommand command = new(waybill.Id, PackagingDate, "WB-1",
            new List<UpdateWaybillItem> { new(product.Id, null, 1) });

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.Inactive.Code);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UpdatesPackagingDateAndWaybillNumber()
    {
        Waybill waybill = Waybill.Create(TenantId, PackagingDate, "WB-1").Value;
        Product product = ProductTestFactory.CreateSingle(stockQuantity: 10);

        SetupRepos(waybill, [product]);
        DateOnly newDate = new(2026, 6, 1);

        UpdateWaybillCommand command = new(waybill.Id, newDate, "WB-NEW",
            new List<UpdateWaybillItem> { new(product.Id, null, 1) });

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        waybill.PackagingDate.Should().Be(newDate);
        waybill.WaybillNumber.Should().Be("WB-NEW");
    }

    private void SetupRepos(Waybill waybill, IReadOnlyList<Product> products)
    {
        _waybillRepository.Setup(r =>
                r.GetByIdWithItemsAsync(waybill.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(waybill);
        _waybillRepository.Setup(r =>
                r.ExistsByNumberInAnyTenantAsync(It.IsAny<string>(), waybill.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _productRepository.Setup(r =>
                r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(products);
        _productRepository.Setup(r =>
                r.GetByBarcodesAsync(It.IsAny<IEnumerable<string>>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(products);
    }
}
