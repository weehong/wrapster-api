using Microsoft.Extensions.Logging.Abstractions;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Shopee.Services;

public class ShopeeTokenRefreshProcessorTests
{
    private const string TenantId = "partner-acme";
    private const long ShopId = 123456;

    private readonly Mock<IShopeeShopConnectionRepository> _connectionRepository = new();
    private readonly Mock<IShopeeGateway> _gateway = new();
    private readonly ShopeeTokenRefreshProcessor _processor;
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public ShopeeTokenRefreshProcessorTests() =>
        _processor = new ShopeeTokenRefreshProcessor(
            _connectionRepository.Object,
            _gateway.Object,
            _unitOfWork.Object,
            NullLogger<ShopeeTokenRefreshProcessor>.Instance);

    private static ShopeeShopConnection CreateConnection(DateTime refreshExpiry) =>
        ShopeeShopConnection.Create(
            TenantId, ShopId, "old-access", "old-refresh",
            DateTime.UtcNow.AddMinutes(30), refreshExpiry, DateTime.UtcNow.AddDays(-1), null).Value;

    private void SetUpConnections(params ShopeeShopConnection[] connections) =>
        _connectionRepository
            .Setup(r => r.ListRequiringRefreshAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(connections);

    [Fact]
    public async Task RunAsync_WhenRefreshSucceeds_UpdatesTokensAndSaves()
    {
        ShopeeShopConnection connection = CreateConnection(DateTime.UtcNow.AddDays(10));
        SetUpConnections(connection);
        _gateway.Setup(g => g.RefreshAccessTokenAsync("old-refresh", ShopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ShopeeTokenGrant>.Success(
                new ShopeeTokenGrant("new-access", "new-refresh", 14400)));

        ShopeeTokenRefreshRunSummary summary = await _processor.RunAsync(CancellationToken.None);

        summary.RefreshedCount.Should().Be(1);
        summary.FailedCount.Should().Be(0);
        summary.ExpiredCount.Should().Be(0);
        connection.AccessToken.Should().Be("new-access");
        connection.RefreshToken.Should().Be("new-refresh");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_WhenRefreshTokenExpired_SkipsWithoutCallingGateway()
    {
        ShopeeShopConnection connection = CreateConnection(DateTime.UtcNow.AddMinutes(-1));
        SetUpConnections(connection);

        ShopeeTokenRefreshRunSummary summary = await _processor.RunAsync(CancellationToken.None);

        summary.ExpiredCount.Should().Be(1);
        summary.RefreshedCount.Should().Be(0);
        _gateway.Verify(
            g => g.RefreshAccessTokenAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_WhenGatewayFails_CountsFailureAndContinues()
    {
        ShopeeShopConnection failing = CreateConnection(DateTime.UtcNow.AddDays(10));
        ShopeeShopConnection succeeding = ShopeeShopConnection.Create(
            "partner-beta", 654321, "old-access", "beta-refresh",
            DateTime.UtcNow.AddMinutes(30), DateTime.UtcNow.AddDays(10), DateTime.UtcNow.AddDays(-1), null).Value;
        SetUpConnections(failing, succeeding);
        _gateway.Setup(g => g.RefreshAccessTokenAsync("old-refresh", ShopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ShopeeTokenGrant>.Failure(ShopeeShopConnectionErrors.TokenRefreshFailed));
        _gateway.Setup(g => g.RefreshAccessTokenAsync("beta-refresh", 654321, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ShopeeTokenGrant>.Success(
                new ShopeeTokenGrant("new-access", "new-refresh", 14400)));

        ShopeeTokenRefreshRunSummary summary = await _processor.RunAsync(CancellationToken.None);

        summary.FailedCount.Should().Be(1);
        summary.RefreshedCount.Should().Be(1);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_WhenNothingExpiring_DoesNothing()
    {
        SetUpConnections();

        ShopeeTokenRefreshRunSummary summary = await _processor.RunAsync(CancellationToken.None);

        summary.Should().Be(new ShopeeTokenRefreshRunSummary(0, 0, 0));
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
