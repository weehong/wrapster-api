using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.FulfillmentDelegations.Commands.CancelFulfillmentDelegation;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.FulfillmentDelegations;

public class CancelFulfillmentDelegationCommandHandlerTests
{
    private const string TenantId = "test-tenant";
    private readonly CancelFulfillmentDelegationCommandHandler _handler;

    private readonly Mock<IFulfillmentDelegationRepository> _repository = new();
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public CancelFulfillmentDelegationCommandHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns(TenantId);
        _handler = new CancelFulfillmentDelegationCommandHandler(
            _repository.Object, _tenantContext.Object, _unitOfWork.Object);
    }

    [Fact]
    public async Task Handle_WhenNotFound_ReturnsNotFound()
    {
        _repository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FulfillmentDelegation?)null);

        Result result = await _handler.Handle(
            new CancelFulfillmentDelegationCommand(), CancellationToken.None);

        result.Error.Code.Should().Be(FulfillmentDelegationErrors.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenActive_CancelsAndSaves()
    {
        FulfillmentDelegation existing =
            FulfillmentDelegation.Request(TenantId, FulfillmentShippingMethod.Dropoff).Value;
        existing.Accept();
        _repository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        Result result = await _handler.Handle(
            new CancelFulfillmentDelegationCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        existing.Status.Should().Be(FulfillmentDelegationStatus.Cancelled);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
