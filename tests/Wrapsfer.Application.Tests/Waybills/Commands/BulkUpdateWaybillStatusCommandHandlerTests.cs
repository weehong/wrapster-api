using Microsoft.Extensions.Options;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Products;
using Wrapsfer.Application.Tests.Helpers;
using Wrapsfer.Application.Waybills.Commands.BulkUpdateWaybillStatus;
using Wrapsfer.Application.Waybills.Responses;
using Wrapsfer.Application.Waybills.Services;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Waybills.Commands;

public class BulkUpdateWaybillStatusCommandHandlerTests
{
    private const string TenantId = "test-tenant";
    private const string UserId = "user-1";
    private static readonly DateOnly TestDate = new(2026, 4, 13);

    private readonly Mock<IProductComponentRepository> _componentRepository = new();
    private readonly BulkUpdateWaybillStatusCommandHandler _handler;
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<ITenantSettingsRepository> _tenantSettingsRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IWaybillRepository> _waybillRepository = new();

    public BulkUpdateWaybillStatusCommandHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns(TenantId);
        _tenantContext.Setup(x => x.UserId).Returns(UserId);
        _tenantContext.Setup(x => x.Roles).Returns(new List<string>());
        StockReservationService stockService =
            new(_productRepository.Object, _componentRepository.Object);
        IOptions<ProductSettings> productSettings = Options.Create(new ProductSettings());
        _handler = new BulkUpdateWaybillStatusCommandHandler(
            _waybillRepository.Object,
            _tenantSettingsRepository.Object,
            stockService,
            _tenantContext.Object,
            _unitOfWork.Object,
            productSettings);
    }

    [Fact]
    public async Task Handle_BulkMarkPacked_SucceedsForDraftWaybills()
    {
        Waybill first = CreateDraftWaybill("WB-1", Guid.NewGuid(), "BC-1", 2);
        Waybill second = CreateDraftWaybill("WB-2", Guid.NewGuid(), "BC-2", 3);
        SetupBatchFetch(first, second);

        Result<BulkWaybillStatusUpdateResult> result = await _handler.Handle(
            new BulkUpdateWaybillStatusCommand([first.Id, second.Id], WaybillStatus.Packed, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalRequested.Should().Be(2);
        result.Value.SuccessCount.Should().Be(2);
        result.Value.FailureCount.Should().Be(0);
        result.Value.Results.Should().OnlyContain(r => r.Success && r.Status == WaybillStatus.Packed);
        first.Status.Should().Be(WaybillStatus.Packed);
        second.Status.Should().Be(WaybillStatus.Packed);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_BulkMarkHandedOff_SucceedsForPackedWaybillCreatedByUser()
    {
        Product product = ProductTestFactory.CreateSingle(barcode: "BC-1", stockQuantity: 10);
        product.Reserve(3);
        Waybill waybill = CreateDraftWaybill("WB-1", product.Id, product.Barcode, 3);
        waybill.SetCreatedBy(UserId);
        waybill.MarkPacked();
        SetupBatchFetch(waybill);
        SetupProducts(product);

        Result<BulkWaybillStatusUpdateResult> result = await _handler.Handle(
            new BulkUpdateWaybillStatusCommand([waybill.Id], WaybillStatus.HandedOff, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.SuccessCount.Should().Be(1);
        waybill.Status.Should().Be(WaybillStatus.HandedOff);
        product.ReservedQuantity.Should().Be(0);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_BulkMarkHandedOff_SucceedsForAdminOnWaybillCreatedByAnotherUser()
    {
        Product product = ProductTestFactory.CreateSingle(barcode: "BC-1", stockQuantity: 10);
        product.Reserve(3);
        Waybill waybill = CreateDraftWaybill("WB-1", product.Id, product.Barcode, 3);
        waybill.SetCreatedBy("another-user");
        waybill.MarkPacked();
        SetupBatchFetch(waybill);
        SetupProducts(product);
        _tenantContext.Setup(x => x.Roles).Returns(new List<string> { "admin" });

        Result<BulkWaybillStatusUpdateResult> result = await _handler.Handle(
            new BulkUpdateWaybillStatusCommand([waybill.Id], WaybillStatus.HandedOff, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.SuccessCount.Should().Be(1);
        waybill.Status.Should().Be(WaybillStatus.HandedOff);
    }

    [Fact]
    public async Task Handle_BulkMarkHandedOff_RaisesLowStockEventUsingTenantFallbackThreshold()
    {
        Product product = ProductTestFactory.CreateSingle(
            barcode: "BC-1", stockQuantity: 12, lowStockThreshold: null);
        product.Reserve(4);
        Waybill waybill = CreateDraftWaybill("WB-1", product.Id, product.Barcode, 4);
        waybill.SetCreatedBy(UserId);
        waybill.MarkPacked();
        SetupBatchFetch(waybill);
        SetupProducts(product);

        Domain.Entities.TenantSettings settings = Domain.Entities.TenantSettings.Create(TenantId, 10).Value;
        _tenantSettingsRepository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        Result<BulkWaybillStatusUpdateResult> result = await _handler.Handle(
            new BulkUpdateWaybillStatusCommand([waybill.Id], WaybillStatus.HandedOff, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        product.StockQuantity.Should().Be(8);
        product.DomainEvents.Should().ContainSingle(e => e is Domain.Events.LowStockDetectedEvent);
    }

    [Fact]
    public async Task Handle_BulkCancel_SucceedsAndReleasesStockForDraftWaybill()
    {
        Product product = ProductTestFactory.CreateSingle(barcode: "BC-1", stockQuantity: 10);
        product.Reserve(4);
        Waybill waybill = CreateDraftWaybill("WB-1", product.Id, product.Barcode, 4);
        SetupBatchFetch(waybill);
        SetupProducts(product);

        Result<BulkWaybillStatusUpdateResult> result = await _handler.Handle(
            new BulkUpdateWaybillStatusCommand([waybill.Id], WaybillStatus.Cancelled, "out of stock"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.SuccessCount.Should().Be(1);
        waybill.Status.Should().Be(WaybillStatus.Cancelled);
        product.ReservedQuantity.Should().Be(0);
        _waybillRepository.Verify(r => r.Detach(It.IsAny<Waybill>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenWaybillMissing_ReturnsFailedItemWithRequestedId()
    {
        Guid missingId = Guid.NewGuid();
        SetupBatchFetch();

        Result<BulkWaybillStatusUpdateResult> result = await _handler.Handle(
            new BulkUpdateWaybillStatusCommand([missingId], WaybillStatus.Packed, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FailureCount.Should().Be(1);
        BulkWaybillStatusUpdateItemResult item = result.Value.Results.Single();
        item.Id.Should().Be(missingId);
        item.Success.Should().BeFalse();
        item.ErrorCode.Should().Be(WaybillErrors.NotFound.Code);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenInvalidTransition_ReturnsFailedItemWithRequestedId()
    {
        Waybill waybill = CreateDraftWaybill("WB-1", Guid.NewGuid(), "BC-1", 2);
        waybill.MarkPacked();
        SetupBatchFetch(waybill);

        Result<BulkWaybillStatusUpdateResult> result = await _handler.Handle(
            new BulkUpdateWaybillStatusCommand([waybill.Id], WaybillStatus.Packed, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        BulkWaybillStatusUpdateItemResult item = result.Value.Results.Single();
        item.Id.Should().Be(waybill.Id);
        item.Success.Should().BeFalse();
        item.Status.Should().BeNull();
        item.ErrorCode.Should().Be(WaybillErrors.InvalidStatusTransition.Code);
    }

    [Fact]
    public async Task Handle_WhenStockConsumeFails_ReturnsFailedItemAndDetachesWaybill()
    {
        Product product = ProductTestFactory.CreateSingle(barcode: "BC-1", stockQuantity: 10);
        product.Reserve(3);
        Waybill waybill = CreateDraftWaybill("WB-1", product.Id, product.Barcode, 3);
        waybill.SetCreatedBy(UserId);
        waybill.MarkPacked();
        SetupBatchFetch(waybill);
        SetupProducts(); // no products returned -> consume fails

        Result<BulkWaybillStatusUpdateResult> result = await _handler.Handle(
            new BulkUpdateWaybillStatusCommand([waybill.Id], WaybillStatus.HandedOff, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FailureCount.Should().Be(1);
        result.Value.Results.Single().Id.Should().Be(waybill.Id);
        result.Value.Results.Single().ErrorCode.Should().Be(ProductErrors.NotFound.Code);
        _waybillRepository.Verify(r => r.Detach(waybill), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenStockReleaseFails_ReturnsFailedItemAndDetachesWaybill()
    {
        Product product = ProductTestFactory.CreateSingle(barcode: "BC-1", stockQuantity: 10);
        product.Reserve(3);
        Waybill waybill = CreateDraftWaybill("WB-1", product.Id, product.Barcode, 3);
        SetupBatchFetch(waybill);
        SetupProducts(); // no products returned -> release fails

        Result<BulkWaybillStatusUpdateResult> result = await _handler.Handle(
            new BulkUpdateWaybillStatusCommand([waybill.Id], WaybillStatus.Cancelled, "damaged"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FailureCount.Should().Be(1);
        result.Value.Results.Single().Id.Should().Be(waybill.Id);
        result.Value.Results.Single().ErrorCode.Should().Be(ProductErrors.NotFound.Code);
        _waybillRepository.Verify(r => r.Detach(waybill), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MixedSuccessAndFailure_ReturnsCorrectCounts()
    {
        Waybill draft = CreateDraftWaybill("WB-1", Guid.NewGuid(), "BC-1", 2);
        Waybill alreadyPacked = CreateDraftWaybill("WB-2", Guid.NewGuid(), "BC-2", 2);
        alreadyPacked.MarkPacked();
        SetupBatchFetch(draft, alreadyPacked);

        Result<BulkWaybillStatusUpdateResult> result = await _handler.Handle(
            new BulkUpdateWaybillStatusCommand([draft.Id, alreadyPacked.Id], WaybillStatus.Packed, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalRequested.Should().Be(2);
        result.Value.SuccessCount.Should().Be(1);
        result.Value.FailureCount.Should().Be(1);
        result.Value.Results.First(r => r.Id == draft.Id).Success.Should().BeTrue();
        result.Value.Results.First(r => r.Id == alreadyPacked.Id).Success.Should().BeFalse();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenDuplicateIdsRequested_ProcessesEachWaybillOnce()
    {
        Waybill waybill = CreateDraftWaybill("WB-1", Guid.NewGuid(), "BC-1", 2);
        SetupBatchFetch(waybill);

        Result<BulkWaybillStatusUpdateResult> result = await _handler.Handle(
            new BulkUpdateWaybillStatusCommand([waybill.Id, waybill.Id], WaybillStatus.Packed, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalRequested.Should().Be(1);
        result.Value.Results.Should().ContainSingle();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Waybill CreateDraftWaybill(string number, Guid productId, string barcode, int quantity)
    {
        Waybill waybill = Waybill.Create(TenantId, TestDate, number).Value;
        waybill.AddOrIncrementItem(productId, barcode, quantity);
        return waybill;
    }

    private void SetupBatchFetch(params Waybill[] waybills) =>
        _waybillRepository.Setup(r => r.GetByIdsWithItemsAsync(
                It.IsAny<IEnumerable<Guid>>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(waybills.ToList());

    private void SetupProducts(params Product[] products) =>
        _productRepository.Setup(r => r.GetByIdsAsync(
                It.IsAny<IEnumerable<Guid>>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(products.ToList());
}
