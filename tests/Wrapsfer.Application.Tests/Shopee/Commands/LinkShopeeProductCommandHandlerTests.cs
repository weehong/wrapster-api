using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Application.Shopee.Commands.LinkShopeeProduct;
using Wrapsfer.Application.Shopee.Responses;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Application.Tests.Helpers;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Shopee.Commands;

public class LinkShopeeProductCommandHandlerTests
{
    private const string TenantId = "test-tenant";
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IShopeeProductLinkRepository> _linkRepository = new();
    private readonly Mock<IShopeeShopConnectionRepository> _connectionRepository = new();
    private readonly Mock<IShopeeGateway> _gateway = new();
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly LinkShopeeProductCommandHandler _handler;

    public LinkShopeeProductCommandHandlerTests()
    {
        _tenantContext.Setup(c => c.Username).Returns("admin@acme");
        _handler = new LinkShopeeProductCommandHandler(
            _productRepository.Object,
            _linkRepository.Object,
            _connectionRepository.Object,
            new ShopeeSellableUnitResolver(_gateway.Object),
            _tenantContext.Object,
            _unitOfWork.Object);
    }

    [Fact]
    public async Task Handle_WhenNoModelItemIsValid_CreatesLink()
    {
        Product product = ProductTestFactory.CreateSingle();
        ShopeeShopConnection connection = CreateConnection();
        SetupProduct(product);
        SetupConnection(connection);
        _gateway.Setup(g => g.GetItemBaseInfoAsync(
                connection.ShopId,
                connection.AccessToken,
                It.Is<IReadOnlyCollection<long>>(ids => ids.Contains(1001)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ShopeeItemDetail>>([
                new ShopeeItemDetail(1001, "Shopee Item", "SKU-1", "NORMAL", false, 9, null)
            ]));

        Result<ShopeeProductLinkResponse> result = await _handler.Handle(
            new LinkShopeeProductCommand(TenantId, product.Id, 1001, 0), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ProductId.Should().Be(product.Id);
        result.Value.ShopeeItemName.Should().Be("Shopee Item");
        result.Value.ShopeeItemSku.Should().Be("SKU-1");
        _linkRepository.Verify(r => r.Add(It.Is<ShopeeProductLink>(
            link => link.ShopeeItemId == 1001 && link.ShopeeModelId == 0)), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenModelItemIsValid_CreatesLinkWithModelSnapshot()
    {
        Product product = ProductTestFactory.CreateSingle();
        ShopeeShopConnection connection = CreateConnection();
        SetupProduct(product);
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

        Result<ShopeeProductLinkResponse> result = await _handler.Handle(
            new LinkShopeeProductCommand(TenantId, product.Id, 1001, 2002), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ShopeeModelName.Should().Be("Large");
        result.Value.ShopeeItemSku.Should().Be("MODEL-SKU");
    }

    [Theory]
    [InlineData("item")]
    [InlineData("product")]
    public async Task Handle_WhenDuplicateExists_FailsBeforeFetchingShopee(string duplicateType)
    {
        Product product = ProductTestFactory.CreateSingle();
        SetupProduct(product);
        if (duplicateType == "product")
        {
            _linkRepository.Setup(r => r.ExistsForProductAsync(TenantId, product.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        }
        else
        {
            _linkRepository.Setup(r => r.ExistsAsync(TenantId, 1001, 0, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        }

        Result<ShopeeProductLinkResponse> result = await _handler.Handle(
            new LinkShopeeProductCommand(TenantId, product.Id, 1001, 0), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(duplicateType == "product"
            ? ShopeeProductLinkErrors.ProductAlreadyLinked
            : ShopeeProductLinkErrors.AlreadyLinked);
        _gateway.Verify(g => g.GetItemBaseInfoAsync(
            It.IsAny<long>(),
            It.IsAny<string>(),
            It.IsAny<IReadOnlyCollection<long>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenProductMissing_Fails()
    {
        Result<ShopeeProductLinkResponse> result = await _handler.Handle(
            new LinkShopeeProductCommand(TenantId, Guid.NewGuid(), 1001, 0), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeProductLinkErrors.ProductNotFound);
    }

    [Fact]
    public async Task Handle_WhenProductInactive_Fails()
    {
        Product product = ProductTestFactory.CreateSingle();
        product.Deactivate(DateTime.UtcNow);
        SetupProduct(product);

        Result<ShopeeProductLinkResponse> result = await _handler.Handle(
            new LinkShopeeProductCommand(TenantId, product.Id, 1001, 0), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeProductLinkErrors.ProductInactive);
    }

    [Fact]
    public async Task Handle_WhenProductIsBundle_Fails()
    {
        Product product = ProductTestFactory.CreateBundle();
        SetupProduct(product);

        Result<ShopeeProductLinkResponse> result = await _handler.Handle(
            new LinkShopeeProductCommand(TenantId, product.Id, 1001, 0), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeProductLinkErrors.BundleProductNotAllowed);
    }

    [Fact]
    public async Task Handle_WhenConnectionMissing_Fails()
    {
        Product product = ProductTestFactory.CreateSingle();
        SetupProduct(product);

        Result<ShopeeProductLinkResponse> result = await _handler.Handle(
            new LinkShopeeProductCommand(TenantId, product.Id, 1001, 0), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeProductLinkErrors.ConnectionNotFound);
    }

    [Fact]
    public async Task Handle_WhenShopeeItemMissing_Fails()
    {
        Product product = ProductTestFactory.CreateSingle();
        ShopeeShopConnection connection = CreateConnection();
        SetupProduct(product);
        SetupConnection(connection);
        _gateway.Setup(g => g.GetItemBaseInfoAsync(
                connection.ShopId,
                connection.AccessToken,
                It.IsAny<IReadOnlyCollection<long>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ShopeeItemDetail>>([]));

        Result<ShopeeProductLinkResponse> result = await _handler.Handle(
            new LinkShopeeProductCommand(TenantId, product.Id, 1001, 0), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeProductLinkErrors.ItemNotFoundInShop);
    }

    [Fact]
    public async Task Handle_WhenModelDoesNotMatchItem_Fails()
    {
        Product product = ProductTestFactory.CreateSingle();
        ShopeeShopConnection connection = CreateConnection();
        SetupProduct(product);
        SetupConnection(connection);
        _gateway.Setup(g => g.GetItemBaseInfoAsync(
                connection.ShopId,
                connection.AccessToken,
                It.IsAny<IReadOnlyCollection<long>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ShopeeItemDetail>>([
                new ShopeeItemDetail(1001, "Shopee Item", "SKU-1", "NORMAL", false, 9, null)
            ]));

        Result<ShopeeProductLinkResponse> result = await _handler.Handle(
            new LinkShopeeProductCommand(TenantId, product.Id, 1001, 2002), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeProductLinkErrors.ModelMismatch);
    }

    private void SetupProduct(Product product) =>
        _productRepository.Setup(r => r.GetByIdAsync(product.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

    private void SetupConnection(ShopeeShopConnection connection) =>
        _connectionRepository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);

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
