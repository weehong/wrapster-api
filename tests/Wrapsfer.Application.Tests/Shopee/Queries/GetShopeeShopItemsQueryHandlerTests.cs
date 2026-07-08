using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Application.Shopee.Queries.GetShopeeShopItems;
using Wrapsfer.Application.Shopee.Responses;
using Wrapsfer.Application.Tests.Helpers;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Shopee.Queries;

public class GetShopeeShopItemsQueryHandlerTests
{
    private const string TenantId = "test-tenant";
    private readonly Mock<IShopeeShopConnectionRepository> _connectionRepository = new();
    private readonly Mock<IShopeeProductLinkRepository> _linkRepository = new();
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IShopeeGateway> _gateway = new();
    private readonly GetShopeeShopItemsQueryHandler _handler;

    public GetShopeeShopItemsQueryHandlerTests() =>
        _handler = new GetShopeeShopItemsQueryHandler(
            _connectionRepository.Object,
            _linkRepository.Object,
            _productRepository.Object,
            _gateway.Object);

    [Fact]
    public async Task Handle_DecoratesLinksAndFetchesModelsSequentially()
    {
        ShopeeShopConnection connection = CreateConnection();
        Product product = ProductTestFactory.CreateSingle(name: "Local Box");
        ShopeeProductLink link = ShopeeProductLink.Create(
            TenantId, product.Id, 1001, 2002, "Shopee Item", "Large", "SKU", null).Value;

        _connectionRepository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);
        _gateway.Setup(g => g.GetItemListAsync(connection.ShopId, connection.AccessToken, 0, 20,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new ShopeeItemPage([1001], true, 20, 30)));
        _gateway.Setup(g => g.GetItemBaseInfoAsync(connection.ShopId, connection.AccessToken,
                It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ShopeeItemDetail>>([
                new ShopeeItemDetail(1001, "Shopee Item", "ITEM-SKU", "NORMAL", true, null, null)
            ]));
        _gateway.Setup(g => g.GetModelListAsync(connection.ShopId, connection.AccessToken, 1001,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ShopeeItemModel>>([
                new ShopeeItemModel(2002, "Large", "SKU", 5)
            ]));
        _linkRepository.Setup(r => r.ListByShopeeItemIdsAsync(TenantId,
                It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([link]);
        _productRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), TenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([product]);

        Result<ShopeeShopItemsResponse> result = await _handler.Handle(
            new GetShopeeShopItemsQuery(TenantId, 0, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.HasNextPage.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Models.Should().ContainSingle();
        result.Value.Items[0].Models[0].Link!.ProductName.Should().Be("Local Box");
    }

    [Fact]
    public async Task Handle_WhenConnectionMissing_Fails()
    {
        Result<ShopeeShopItemsResponse> result = await _handler.Handle(
            new GetShopeeShopItemsQuery(TenantId, 0, 20), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeProductLinkErrors.ConnectionNotFound);
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
