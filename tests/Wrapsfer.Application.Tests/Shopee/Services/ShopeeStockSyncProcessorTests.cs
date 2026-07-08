using Microsoft.Extensions.Logging.Abstractions;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Application.Tests.Helpers;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Shopee.Services;

public class ShopeeStockSyncProcessorTests
{
    private const string TenantId = "test-tenant";
    private readonly Mock<IShopeeProductLinkRepository> _linkRepository = new();
    private readonly Mock<IShopeeShopConnectionRepository> _connectionRepository = new();
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IShopeeGateway> _gateway = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly ShopeeStockSyncProcessor _processor;

    public ShopeeStockSyncProcessorTests() =>
        _processor = new ShopeeStockSyncProcessor(
            _linkRepository.Object,
            _connectionRepository.Object,
            _productRepository.Object,
            _gateway.Object,
            _unitOfWork.Object,
            NullLogger<ShopeeStockSyncProcessor>.Instance);

    [Fact]
    public async Task RunForTenantAsync_WhenLinkIsActive_PushesAvailableQuantityAndMarksSynced()
    {
        Product product = ProductTestFactory.CreateSingle(stockQuantity: 10);
        product.Reserve(3);
        ShopeeProductLink link = CreateLink(product.Id);
        ShopeeShopConnection connection = CreateConnection(DateTime.UtcNow.AddHours(1));
        SetupManualRun(connection, link, product);
        _gateway.Setup(g => g.UpdateStockAsync(connection.ShopId, connection.AccessToken, 1001, 0, 7,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        Result<ShopeeStockSyncRunSummary> result = await _processor.RunForTenantAsync(
            TenantId, link.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.SyncedCount.Should().Be(1);
        link.LastSyncedQuantity.Should().Be(7);
        link.LastSyncError.Should().BeNull();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunForTenantAsync_WhenProductInactive_PushesZero()
    {
        Product product = ProductTestFactory.CreateSingle(stockQuantity: 10);
        product.Deactivate(DateTime.UtcNow);
        ShopeeProductLink link = CreateLink(product.Id);
        ShopeeShopConnection connection = CreateConnection(DateTime.UtcNow.AddHours(1));
        SetupManualRun(connection, link, product);
        _gateway.Setup(g => g.UpdateStockAsync(connection.ShopId, connection.AccessToken, 1001, 0, 0,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        Result<ShopeeStockSyncRunSummary> result = await _processor.RunForTenantAsync(
            TenantId, link.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.SyncedCount.Should().Be(1);
        link.LastSyncedQuantity.Should().Be(0);
    }

    [Fact]
    public async Task RunForTenantAsync_WhenTokenNearExpiry_RefreshesBeforePush()
    {
        Product product = ProductTestFactory.CreateSingle(stockQuantity: 5);
        ShopeeProductLink link = CreateLink(product.Id);
        ShopeeShopConnection connection = CreateConnection(DateTime.UtcNow.AddMinutes(1));
        SetupManualRun(connection, link, product);
        _gateway.Setup(g => g.RefreshAccessTokenAsync("refresh-token", connection.ShopId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new ShopeeTokenGrant("new-access", "new-refresh", 14400)));
        _gateway.Setup(g => g.UpdateStockAsync(connection.ShopId, "new-access", 1001, 0, 5,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        Result<ShopeeStockSyncRunSummary> result = await _processor.RunForTenantAsync(
            TenantId, link.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        connection.AccessToken.Should().Be("new-access");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RunForTenantAsync_WhenAuthFails_RefreshesAndRetriesOnce()
    {
        Product product = ProductTestFactory.CreateSingle(stockQuantity: 5);
        ShopeeProductLink link = CreateLink(product.Id);
        ShopeeShopConnection connection = CreateConnection(DateTime.UtcNow.AddHours(1));
        SetupManualRun(connection, link, product);
        _gateway.SetupSequence(g => g.UpdateStockAsync(connection.ShopId, It.IsAny<string>(), 1001, 0, 5,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(ShopeeProductLinkErrors.AuthFailed))
            .ReturnsAsync(Result.Success());
        _gateway.Setup(g => g.RefreshAccessTokenAsync("refresh-token", connection.ShopId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new ShopeeTokenGrant("new-access", "new-refresh", 14400)));

        Result<ShopeeStockSyncRunSummary> result = await _processor.RunForTenantAsync(
            TenantId, link.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.SyncedCount.Should().Be(1);
        _gateway.Verify(g => g.UpdateStockAsync(connection.ShopId, It.IsAny<string>(), 1001, 0, 5,
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RunForTenantAsync_WhenPushFails_MarksFailed()
    {
        Product product = ProductTestFactory.CreateSingle(stockQuantity: 5);
        ShopeeProductLink link = CreateLink(product.Id);
        ShopeeShopConnection connection = CreateConnection(DateTime.UtcNow.AddHours(1));
        SetupManualRun(connection, link, product);
        _gateway.Setup(g => g.UpdateStockAsync(connection.ShopId, connection.AccessToken, 1001, 0, 5,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(ShopeeProductLinkErrors.StockPushFailed));

        Result<ShopeeStockSyncRunSummary> result = await _processor.RunForTenantAsync(
            TenantId, link.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FailedCount.Should().Be(1);
        link.SyncFailureCount.Should().Be(1);
        link.LastSyncError.Should().Be(ShopeeProductLinkErrors.StockPushFailed.Code);
        link.NextSyncEligibleAt.Should().NotBeNull();
    }

    private void SetupManualRun(
        ShopeeShopConnection connection,
        ShopeeProductLink link,
        Product product)
    {
        _connectionRepository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);
        _linkRepository.Setup(r => r.GetByIdAsync(link.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(link);
        _productRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), TenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([product]);
    }

    private static ShopeeProductLink CreateLink(Guid productId) =>
        ShopeeProductLink.Create(TenantId, productId, 1001, 0, "Item", null, null, null).Value;

    private static ShopeeShopConnection CreateConnection(DateTime accessTokenExpiresAt) =>
        ShopeeShopConnection.Create(
            TenantId,
            123456,
            "access-token",
            "refresh-token",
            accessTokenExpiresAt,
            DateTime.UtcNow.AddDays(30),
            DateTime.UtcNow,
            null).Value;
}
