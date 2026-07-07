using Wrapsfer.Application.Shopee.Queries.GetShopeeConnection;
using Wrapsfer.Application.Shopee.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Shopee.Queries;

public class GetShopeeConnectionQueryHandlerTests
{
    private const string TenantId = "partner-acme";

    private readonly Mock<IShopeeShopConnectionRepository> _connectionRepository = new();
    private readonly GetShopeeConnectionQueryHandler _handler;

    public GetShopeeConnectionQueryHandlerTests() =>
        _handler = new GetShopeeConnectionQueryHandler(_connectionRepository.Object);

    [Fact]
    public async Task Handle_WhenLinked_ReturnsConnectionWithoutTokens()
    {
        ShopeeShopConnection connection = ShopeeShopConnection.Create(
            TenantId, 123456, "access-token", "refresh-token",
            DateTime.UtcNow.AddHours(4), DateTime.UtcNow.AddDays(30), DateTime.UtcNow, null).Value;
        connection.UpdateShopProfile("Acme Store", "SG");
        _connectionRepository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);

        Result<ShopeeConnectionResponse> result = await _handler.Handle(
            new GetShopeeConnectionQuery(TenantId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ShopId.Should().Be(123456);
        result.Value.ShopName.Should().Be("Acme Store");
        result.Value.Region.Should().Be("SG");
    }

    [Fact]
    public async Task Handle_WhenNotLinked_FailsWithNotFound()
    {
        Result<ShopeeConnectionResponse> result = await _handler.Handle(
            new GetShopeeConnectionQuery(TenantId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeShopConnectionErrors.NotFound);
    }
}
