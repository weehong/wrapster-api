using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.FulfillmentDelegations.Queries.GetFulfillmentDelegation;
using Wrapsfer.Application.FulfillmentDelegations.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.FulfillmentDelegations;

public class GetFulfillmentDelegationQueryHandlerTests
{
    private const string TenantId = "test-tenant";
    private readonly GetFulfillmentDelegationQueryHandler _handler;

    private readonly Mock<IFulfillmentDelegationRepository> _repository = new();
    private readonly Mock<ITenantContext> _tenantContext = new();

    public GetFulfillmentDelegationQueryHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns(TenantId);
        _handler = new GetFulfillmentDelegationQueryHandler(_repository.Object, _tenantContext.Object);
    }

    [Fact]
    public async Task Handle_WhenNotFound_ReturnsNotFound()
    {
        _repository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FulfillmentDelegation?)null);

        Result<FulfillmentDelegationResponse> result = await _handler.Handle(
            new GetFulfillmentDelegationQuery(), CancellationToken.None);

        result.Error.Code.Should().Be(FulfillmentDelegationErrors.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenExists_ReturnsResponse()
    {
        FulfillmentDelegation existing =
            FulfillmentDelegation.Request(TenantId, FulfillmentShippingMethod.Pickup).Value;
        _repository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        Result<FulfillmentDelegationResponse> result = await _handler.Handle(
            new GetFulfillmentDelegationQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TenantId.Should().Be(TenantId);
        result.Value.DefaultShippingMethod.Should().Be(FulfillmentShippingMethod.Pickup);
    }
}
