using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Application.Products;
using Wrapsfer.Application.Shopee.Commands.CreateShopeeLinkedProduct;
using Wrapsfer.Application.Shopee.Responses;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Application.Tests.Helpers;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Events;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Shopee.Commands;

public class CreateShopeeLinkedProductCommandHandlerTests
{
    private const string TenantId = "test-tenant";
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IShopeeProductLinkRepository> _linkRepository = new();
    private readonly Mock<IShopeeShopConnectionRepository> _connectionRepository = new();
    private readonly Mock<IShopeeGateway> _gateway = new();
    private readonly Mock<ITenantSettingsRepository> _tenantSettingsRepository = new();
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly CreateShopeeLinkedProductCommandHandler _handler;

    public CreateShopeeLinkedProductCommandHandlerTests()
    {
        _tenantContext.Setup(c => c.Username).Returns("admin@acme");
        _tenantSettingsRepository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Entities.TenantSettings?)null);
        IOptions<ProductSettings> settings = Options.Create(new ProductSettings { GlobalLowStockThreshold = 10 });
        _handler = new CreateShopeeLinkedProductCommandHandler(
            _productRepository.Object,
            _linkRepository.Object,
            _connectionRepository.Object,
            new ShopeeSellableUnitResolver(_gateway.Object),
            _tenantSettingsRepository.Object,
            _tenantContext.Object,
            _unitOfWork.Object,
            settings);
    }

    [Fact]
    public async Task Handle_WhenValid_CreatesProductAndLinkAtomically()
    {
        ShopeeShopConnection connection = CreateConnection();
        SetupConnection(connection);
        SetupModellessItem(connection, stockQuantity: 9);
        Product? addedProduct = null;
        ShopeeProductLink? addedLink = null;
        _productRepository.Setup(r => r.Add(It.IsAny<Product>()))
            .Callback<Product>(p => addedProduct = p);
        _linkRepository.Setup(r => r.Add(It.IsAny<ShopeeProductLink>()))
            .Callback<ShopeeProductLink>(l => addedLink = l);

        Result<ShopeeProductLinkResponse> result = await _handler.Handle(
            CreateCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        addedProduct.Should().NotBeNull();
        addedProduct!.Name.Should().Be("Local Widget");
        addedProduct.Barcode.Should().Be("BC-100");
        addedProduct.SkuCode.Should().Be("LOCAL-SKU");
        addedProduct.StockQuantity.Should().Be(9);
        addedProduct.Type.Should().Be(ProductType.Single);
        addedLink.Should().NotBeNull();
        addedLink!.ProductId.Should().Be(addedProduct.Id);
        addedLink.ShopeeItemId.Should().Be(1001);
        addedLink.ShopeeItemName.Should().Be("Shopee Item");
        addedLink.ShopeeItemSku.Should().Be("SHOPEE-SKU");
        addedLink.LinkedBy.Should().Be("admin@acme");
        result.Value.ProductName.Should().Be("Local Widget");
        result.Value.ShopeeItemName.Should().Be("Shopee Item");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenModelItem_UsesModelSnapshotForLink()
    {
        ShopeeShopConnection connection = CreateConnection();
        SetupConnection(connection);
        _gateway.Setup(g => g.GetItemBaseInfoAsync(
                connection.ShopId,
                connection.AccessToken,
                It.IsAny<IReadOnlyCollection<long>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ShopeeItemDetail>>([
                new ShopeeItemDetail(1001, "Shopee Item", "ITEM-SKU", "NORMAL", true, null, null)
            ]));
        _gateway.Setup(g => g.GetModelListAsync(
                connection.ShopId,
                connection.AccessToken,
                1001,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ShopeeItemModel>>([
                new ShopeeItemModel(2002, "Large", "MODEL-SKU", 5)
            ]));
        ShopeeProductLink? addedLink = null;
        _linkRepository.Setup(r => r.Add(It.IsAny<ShopeeProductLink>()))
            .Callback<ShopeeProductLink>(l => addedLink = l);

        Result<ShopeeProductLinkResponse> result = await _handler.Handle(
            CreateCommand(modelId: 2002), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        addedLink.Should().NotBeNull();
        addedLink!.ShopeeModelId.Should().Be(2002);
        addedLink.ShopeeModelName.Should().Be("Large");
        addedLink.ShopeeItemSku.Should().Be("MODEL-SKU");
    }

    [Fact]
    public async Task Handle_WhenStockBelowThreshold_RaisesLowStockEvent()
    {
        ShopeeShopConnection connection = CreateConnection();
        SetupConnection(connection);
        SetupModellessItem(connection, stockQuantity: 2);
        Product? addedProduct = null;
        _productRepository.Setup(r => r.Add(It.IsAny<Product>()))
            .Callback<Product>(p => addedProduct = p);

        Result<ShopeeProductLinkResponse> result = await _handler.Handle(
            CreateCommand(stockQuantity: 2, lowStockThreshold: 5), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        addedProduct.Should().NotBeNull();
        addedProduct!.DomainEvents.OfType<LowStockDetectedEvent>().Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_WhenUnitAlreadyLinked_FailsBeforeFetchingShopee()
    {
        _linkRepository.Setup(r => r.ExistsAsync(TenantId, 1001, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Result<ShopeeProductLinkResponse> result = await _handler.Handle(
            CreateCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeProductLinkErrors.AlreadyLinked);
        _gateway.Verify(g => g.GetItemBaseInfoAsync(
            It.IsAny<long>(),
            It.IsAny<string>(),
            It.IsAny<IReadOnlyCollection<long>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        VerifyNothingCreated();
    }

    [Fact]
    public async Task Handle_WhenBarcodeExists_FailsBeforeFetchingShopee()
    {
        Product existing = ProductTestFactory.CreateSingle();
        _productRepository.Setup(r => r.GetByBarcodeAsync("BC-100", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        Result<ShopeeProductLinkResponse> result = await _handler.Handle(
            CreateCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.BarcodeAlreadyExists);
        _gateway.Verify(g => g.GetItemBaseInfoAsync(
            It.IsAny<long>(),
            It.IsAny<string>(),
            It.IsAny<IReadOnlyCollection<long>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        VerifyNothingCreated();
    }

    [Fact]
    public async Task Handle_WhenSkuExists_Fails()
    {
        Product existing = ProductTestFactory.CreateSingle(skuCode: "LOCAL-SKU");
        _productRepository.Setup(r => r.GetBySkuCodeAsync("LOCAL-SKU", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        Result<ShopeeProductLinkResponse> result = await _handler.Handle(
            CreateCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.SkuAlreadyExists);
        VerifyNothingCreated();
    }

    [Fact]
    public async Task Handle_WhenConnectionMissing_Fails()
    {
        Result<ShopeeProductLinkResponse> result = await _handler.Handle(
            CreateCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeProductLinkErrors.ConnectionNotFound);
        VerifyNothingCreated();
    }

    [Fact]
    public async Task Handle_WhenShopeeItemMissing_Fails()
    {
        ShopeeShopConnection connection = CreateConnection();
        SetupConnection(connection);
        _gateway.Setup(g => g.GetItemBaseInfoAsync(
                connection.ShopId,
                connection.AccessToken,
                It.IsAny<IReadOnlyCollection<long>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ShopeeItemDetail>>([]));

        Result<ShopeeProductLinkResponse> result = await _handler.Handle(
            CreateCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeProductLinkErrors.ItemNotFoundInShop);
        VerifyNothingCreated();
    }

    private static CreateShopeeLinkedProductCommand CreateCommand(
        long itemId = 1001,
        long modelId = 0,
        string barcode = "BC-100",
        string name = "Local Widget",
        string? skuCode = "LOCAL-SKU",
        decimal cost = 12.5m,
        int stockQuantity = 9,
        int? lowStockThreshold = null) =>
        new(TenantId, itemId, modelId, barcode, name, skuCode, cost, stockQuantity, lowStockThreshold);

    private void SetupConnection(ShopeeShopConnection connection) =>
        _connectionRepository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);

    private void SetupModellessItem(ShopeeShopConnection connection, int stockQuantity) =>
        _gateway.Setup(g => g.GetItemBaseInfoAsync(
                connection.ShopId,
                connection.AccessToken,
                It.Is<IReadOnlyCollection<long>>(ids => ids.Contains(1001)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ShopeeItemDetail>>([
                new ShopeeItemDetail(1001, "Shopee Item", "SHOPEE-SKU", "NORMAL", false, stockQuantity, null)
            ]));

    private void VerifyNothingCreated()
    {
        _productRepository.Verify(r => r.Add(It.IsAny<Product>()), Times.Never);
        _linkRepository.Verify(r => r.Add(It.IsAny<ShopeeProductLink>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ShopeeShopConnection CreateConnection() =>
        ShopeeShopConnection.Create(
            TenantId,
            123456,
            "access-token",
            "refresh-token",
            DateTime.UtcNow.AddHours(4),
            DateTime.UtcNow.AddDays(30),
            DateTime.UtcNow,
            null).Value;
}
