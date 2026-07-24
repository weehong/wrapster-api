using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.FulfillmentDelegations.Commands.RequestFulfillmentDelegation;
using Wrapsfer.Application.FulfillmentDelegations.Responses;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.FulfillmentDelegations;

public class RequestFulfillmentDelegationCommandHandlerTests
{
    private const string TenantId = "test-tenant";
    private readonly RequestFulfillmentDelegationCommandHandler _handler;

    private readonly Mock<IFulfillmentDelegationRepository> _repository = new();
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public RequestFulfillmentDelegationCommandHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns(TenantId);
        _handler = new RequestFulfillmentDelegationCommandHandler(
            _repository.Object, _tenantContext.Object, _unitOfWork.Object);
    }

    [Fact]
    public async Task Handle_WhenNoExisting_CreatesAndSaves()
    {
        _repository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FulfillmentDelegation?)null);

        Result<FulfillmentDelegationResponse> result = await _handler.Handle(
            new RequestFulfillmentDelegationCommand(FulfillmentShippingMethod.Dropoff),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(FulfillmentDelegationStatus.Requested);
        result.Value.TenantId.Should().Be(TenantId);
        _repository.Verify(r => r.Add(It.Is<FulfillmentDelegation>(d => d.TenantId == TenantId)), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenDeclined_ReRequests()
    {
        FulfillmentDelegation existing =
            FulfillmentDelegation.Request(TenantId, FulfillmentShippingMethod.Dropoff).Value;
        existing.Decline("no contract");
        _repository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        Result<FulfillmentDelegationResponse> result = await _handler.Handle(
            new RequestFulfillmentDelegationCommand(FulfillmentShippingMethod.Pickup),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(FulfillmentDelegationStatus.Requested);
        result.Value.DefaultShippingMethod.Should().Be(FulfillmentShippingMethod.Pickup);
        _repository.Verify(r => r.Add(It.IsAny<FulfillmentDelegation>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAlreadyRequested_ReturnsConflict()
    {
        FulfillmentDelegation existing =
            FulfillmentDelegation.Request(TenantId, FulfillmentShippingMethod.Dropoff).Value;
        _repository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        Result<FulfillmentDelegationResponse> result = await _handler.Handle(
            new RequestFulfillmentDelegationCommand(FulfillmentShippingMethod.Dropoff),
            CancellationToken.None);

        result.Error.Code.Should().Be(FulfillmentDelegationErrors.AlreadyRequested.Code);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenActive_ReturnsConflict()
    {
        FulfillmentDelegation existing =
            FulfillmentDelegation.Request(TenantId, FulfillmentShippingMethod.Dropoff).Value;
        existing.Accept();
        _repository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        Result<FulfillmentDelegationResponse> result = await _handler.Handle(
            new RequestFulfillmentDelegationCommand(FulfillmentShippingMethod.Dropoff),
            CancellationToken.None);

        result.Error.Code.Should().Be(FulfillmentDelegationErrors.AlreadyActive.Code);
    }
}
