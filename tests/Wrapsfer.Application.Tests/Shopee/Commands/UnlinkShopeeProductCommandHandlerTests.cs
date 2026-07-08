using Wrapsfer.Application.Shopee.Commands.UnlinkShopeeProduct;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Shopee.Commands;

public class UnlinkShopeeProductCommandHandlerTests
{
    private const string TenantId = "test-tenant";
    private readonly Mock<IShopeeProductLinkRepository> _linkRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly UnlinkShopeeProductCommandHandler _handler;

    public UnlinkShopeeProductCommandHandlerTests() =>
        _handler = new UnlinkShopeeProductCommandHandler(_linkRepository.Object, _unitOfWork.Object);

    [Fact]
    public async Task Handle_WhenLinkExists_RemovesIt()
    {
        ShopeeProductLink link = CreateLink();
        _linkRepository.Setup(r => r.GetByIdAsync(link.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(link);

        Result result = await _handler.Handle(
            new UnlinkShopeeProductCommand(TenantId, link.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _linkRepository.Verify(r => r.Remove(link), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenLinkMissing_Fails()
    {
        Result result = await _handler.Handle(
            new UnlinkShopeeProductCommand(TenantId, Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeProductLinkErrors.NotFound);
    }

    private static ShopeeProductLink CreateLink() =>
        ShopeeProductLink.Create(TenantId, Guid.NewGuid(), 1001, 0, "Item", null, null, null).Value;
}
