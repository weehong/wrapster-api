using Wrapsfer.Application.Waybills.Queries.CheckWaybillNumberAvailable;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Waybills.Queries;

public class CheckWaybillNumberAvailableQueryHandlerTests
{
    private readonly CheckWaybillNumberAvailableQueryHandler _handler;
    private readonly Mock<IWaybillRepository> _waybillRepository = new();

    public CheckWaybillNumberAvailableQueryHandlerTests() =>
        _handler = new CheckWaybillNumberAvailableQueryHandler(_waybillRepository.Object);

    [Fact]
    public async Task Handle_WhenNumberExistsInAnyTenant_ReturnsFalse()
    {
        _waybillRepository.Setup(r =>
                r.ExistsByNumberInAnyTenantAsync("WB-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Result<bool> result = await _handler.Handle(
            new CheckWaybillNumberAvailableQuery("WB-1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenNumberDoesNotExist_ReturnsTrue()
    {
        _waybillRepository.Setup(r =>
                r.ExistsByNumberInAnyTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Result<bool> result = await _handler.Handle(
            new CheckWaybillNumberAvailableQuery("WB-1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_PassesTrimmedNumberToRepository()
    {
        _waybillRepository.Setup(r =>
                r.ExistsByNumberInAnyTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _handler.Handle(
            new CheckWaybillNumberAvailableQuery(" WB-1 "), CancellationToken.None);

        _waybillRepository.Verify(r =>
            r.ExistsByNumberInAnyTenantAsync("WB-1", It.IsAny<CancellationToken>()), Times.Once);
    }
}
