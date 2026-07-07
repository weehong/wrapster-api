using Microsoft.Extensions.Logging.Abstractions;
using Wrapsfer.Application.Shopee.Commands.DisconnectShopeeShop;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Shopee.Commands;

public class DisconnectShopeeShopCommandHandlerTests
{
    private const string TenantId = "partner-acme";

    private readonly Mock<IShopeeShopConnectionRepository> _connectionRepository = new();
    private readonly DisconnectShopeeShopCommandHandler _handler;
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public DisconnectShopeeShopCommandHandlerTests() =>
        _handler = new DisconnectShopeeShopCommandHandler(
            _connectionRepository.Object,
            _unitOfWork.Object,
            NullLogger<DisconnectShopeeShopCommandHandler>.Instance);

    private static ShopeeShopConnection CreateConnection() =>
        ShopeeShopConnection.Create(
            TenantId, 123456, "access-token", "refresh-token",
            DateTime.UtcNow.AddHours(4), DateTime.UtcNow.AddDays(30), DateTime.UtcNow, null).Value;

    [Fact]
    public async Task Handle_WhenLinked_RemovesConnection()
    {
        ShopeeShopConnection connection = CreateConnection();
        _connectionRepository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);

        Result result = await _handler.Handle(
            new DisconnectShopeeShopCommand(TenantId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _connectionRepository.Verify(r => r.Remove(connection), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNotLinked_FailsWithNotFound()
    {
        Result result = await _handler.Handle(
            new DisconnectShopeeShopCommand(TenantId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeShopConnectionErrors.NotFound);
        _connectionRepository.Verify(r => r.Remove(It.IsAny<ShopeeShopConnection>()), Times.Never);
    }
}
