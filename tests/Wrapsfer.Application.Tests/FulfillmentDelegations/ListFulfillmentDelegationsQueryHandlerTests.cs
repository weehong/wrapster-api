using Wrapsfer.Application.FulfillmentDelegations.Queries.ListFulfillmentDelegations;
using Wrapsfer.Application.FulfillmentDelegations.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.FulfillmentDelegations;

public class ListFulfillmentDelegationsQueryHandlerTests
{
    private readonly ListFulfillmentDelegationsQueryHandler _handler;

    private readonly Mock<IFulfillmentDelegationRepository> _repository = new();

    public ListFulfillmentDelegationsQueryHandlerTests() =>
        _handler = new ListFulfillmentDelegationsQueryHandler(_repository.Object);

    [Fact]
    public async Task Handle_PassesStatusFilterAndMapsResponses()
    {
        FulfillmentDelegation delegation =
            FulfillmentDelegation.Request("partner-a", FulfillmentShippingMethod.Dropoff).Value;
        _repository.Setup(r => r.ListAsync(FulfillmentDelegationStatus.Requested, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FulfillmentDelegation> { delegation });

        Result<IReadOnlyList<FulfillmentDelegationResponse>> result = await _handler.Handle(
            new ListFulfillmentDelegationsQuery(FulfillmentDelegationStatus.Requested),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(d => d.TenantId == "partner-a");
    }

    [Fact]
    public async Task Handle_WithNoStatus_ListsAll()
    {
        _repository.Setup(r => r.ListAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FulfillmentDelegation>());

        Result<IReadOnlyList<FulfillmentDelegationResponse>> result = await _handler.Handle(
            new ListFulfillmentDelegationsQuery(null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
