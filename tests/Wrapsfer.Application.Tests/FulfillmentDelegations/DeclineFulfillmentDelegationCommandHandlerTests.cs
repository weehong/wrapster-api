using Wrapsfer.Application.FulfillmentDelegations.Commands.DeclineFulfillmentDelegation;
using Wrapsfer.Application.FulfillmentDelegations.Responses;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.FulfillmentDelegations;

public class DeclineFulfillmentDelegationCommandHandlerTests
{
    private const string TenantId = "partner-a";
    private readonly DeclineFulfillmentDelegationCommandHandler _handler;

    private readonly Mock<IFulfillmentDelegationRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public DeclineFulfillmentDelegationCommandHandlerTests() =>
        _handler = new DeclineFulfillmentDelegationCommandHandler(_repository.Object, _unitOfWork.Object);

    [Fact]
    public async Task Handle_WhenNotFound_ReturnsNotFound()
    {
        _repository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FulfillmentDelegation?)null);

        Result<FulfillmentDelegationResponse> result = await _handler.Handle(
            new DeclineFulfillmentDelegationCommand(TenantId, "reason"), CancellationToken.None);

        result.Error.Code.Should().Be(FulfillmentDelegationErrors.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenRequested_DeclinesWithReasonAndSaves()
    {
        FulfillmentDelegation existing =
            FulfillmentDelegation.Request(TenantId, FulfillmentShippingMethod.Dropoff).Value;
        _repository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        Result<FulfillmentDelegationResponse> result = await _handler.Handle(
            new DeclineFulfillmentDelegationCommand(TenantId, "No signed contract"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(FulfillmentDelegationStatus.Declined);
        result.Value.DeclineReason.Should().Be("No signed contract");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
