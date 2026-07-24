using Wrapsfer.Application.FulfillmentDelegations.Commands.RevokeFulfillmentDelegation;
using Wrapsfer.Application.FulfillmentDelegations.Responses;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.FulfillmentDelegations;

public class RevokeFulfillmentDelegationCommandHandlerTests
{
    private const string TenantId = "partner-a";
    private readonly RevokeFulfillmentDelegationCommandHandler _handler;

    private readonly Mock<IFulfillmentDelegationRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public RevokeFulfillmentDelegationCommandHandlerTests() =>
        _handler = new RevokeFulfillmentDelegationCommandHandler(_repository.Object, _unitOfWork.Object);

    [Fact]
    public async Task Handle_WhenNotFound_ReturnsNotFound()
    {
        _repository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FulfillmentDelegation?)null);

        Result<FulfillmentDelegationResponse> result = await _handler.Handle(
            new RevokeFulfillmentDelegationCommand(TenantId, "reason"), CancellationToken.None);

        result.Error.Code.Should().Be(FulfillmentDelegationErrors.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenActive_RevokesWithReasonAndSaves()
    {
        FulfillmentDelegation existing =
            FulfillmentDelegation.Request(TenantId, FulfillmentShippingMethod.Dropoff).Value;
        existing.Accept();
        _repository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        Result<FulfillmentDelegationResponse> result = await _handler.Handle(
            new RevokeFulfillmentDelegationCommand(TenantId, "Contract terminated"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(FulfillmentDelegationStatus.Revoked);
        result.Value.RevokeReason.Should().Be("Contract terminated");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNotActive_ReturnsConflict()
    {
        FulfillmentDelegation existing =
            FulfillmentDelegation.Request(TenantId, FulfillmentShippingMethod.Dropoff).Value;
        _repository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        Result<FulfillmentDelegationResponse> result = await _handler.Handle(
            new RevokeFulfillmentDelegationCommand(TenantId, "reason"), CancellationToken.None);

        result.Error.Code.Should().Be(FulfillmentDelegationErrors.NotActive.Code);
    }
}
