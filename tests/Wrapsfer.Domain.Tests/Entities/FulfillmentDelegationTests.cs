using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Events;

namespace Wrapsfer.Domain.Tests.Entities;

public class FulfillmentDelegationTests
{
    private const string TenantId = "test-tenant";

    private static FulfillmentDelegation CreateRequested(
        FulfillmentShippingMethod method = FulfillmentShippingMethod.Dropoff) =>
        FulfillmentDelegation.Request(TenantId, method).Value;

    private static FulfillmentDelegation CreateActive()
    {
        FulfillmentDelegation delegation = CreateRequested();
        delegation.Accept();
        return delegation;
    }

    [Fact]
    public void Request_WithValidData_CreatesRequestedAndRaisesEvent()
    {
        Result<FulfillmentDelegation> result =
            FulfillmentDelegation.Request(TenantId, FulfillmentShippingMethod.Pickup);

        result.IsSuccess.Should().BeTrue();
        FulfillmentDelegation delegation = result.Value;
        delegation.Status.Should().Be(FulfillmentDelegationStatus.Requested);
        delegation.TenantId.Should().Be(TenantId);
        delegation.DefaultShippingMethod.Should().Be(FulfillmentShippingMethod.Pickup);
        delegation.RequestedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        delegation.AcceptedAt.Should().BeNull();
        delegation.DomainEvents.Should().ContainSingle(e => e is FulfillmentDelegationRequestedEvent);
    }

    [Fact]
    public void Request_WithBlankTenantId_Fails()
    {
        Result<FulfillmentDelegation> result =
            FulfillmentDelegation.Request("  ", FulfillmentShippingMethod.Dropoff);

        result.Error.Code.Should().Be(FulfillmentDelegationErrors.InvalidTenantId.Code);
    }

    [Fact]
    public void Accept_WhenRequested_TransitionsToActive()
    {
        FulfillmentDelegation delegation = CreateRequested();

        Result result = delegation.Accept();

        result.IsSuccess.Should().BeTrue();
        delegation.Status.Should().Be(FulfillmentDelegationStatus.Active);
        delegation.AcceptedAt.Should().NotBeNull();
        delegation.DomainEvents.Should().ContainSingle(e => e is FulfillmentDelegationAcceptedEvent);
    }

    [Fact]
    public void Accept_WhenNotRequested_Fails()
    {
        FulfillmentDelegation delegation = CreateActive();

        Result result = delegation.Accept();

        result.Error.Code.Should().Be(FulfillmentDelegationErrors.NotPending.Code);
    }

    [Fact]
    public void Decline_WhenRequested_SetsReasonAndDeclines()
    {
        FulfillmentDelegation delegation = CreateRequested();

        Result result = delegation.Decline("No signed contract on file");

        result.IsSuccess.Should().BeTrue();
        delegation.Status.Should().Be(FulfillmentDelegationStatus.Declined);
        delegation.DeclineReason.Should().Be("No signed contract on file");
        delegation.DeclinedAt.Should().NotBeNull();
        delegation.DomainEvents.Should().ContainSingle(e => e is FulfillmentDelegationDeclinedEvent);
    }

    [Fact]
    public void Decline_WithoutReason_Fails()
    {
        FulfillmentDelegation delegation = CreateRequested();

        Result result = delegation.Decline("  ");

        result.Error.Code.Should().Be(FulfillmentDelegationErrors.DeclineReasonRequired.Code);
        delegation.Status.Should().Be(FulfillmentDelegationStatus.Requested);
    }

    [Fact]
    public void Decline_TruncatesLongReason()
    {
        FulfillmentDelegation delegation = CreateRequested();

        delegation.Decline(new string('x', 1500));

        delegation.DeclineReason.Should().HaveLength(FulfillmentDelegation.ReasonMaxLength);
    }

    [Fact]
    public void Decline_WhenActive_Fails()
    {
        FulfillmentDelegation delegation = CreateActive();

        Result result = delegation.Decline("reason");

        result.Error.Code.Should().Be(FulfillmentDelegationErrors.NotPending.Code);
    }

    [Fact]
    public void Revoke_WhenActive_TransitionsToRevoked()
    {
        FulfillmentDelegation delegation = CreateActive();

        Result result = delegation.Revoke("Contract terminated");

        result.IsSuccess.Should().BeTrue();
        delegation.Status.Should().Be(FulfillmentDelegationStatus.Revoked);
        delegation.RevokeReason.Should().Be("Contract terminated");
        delegation.RevokedAt.Should().NotBeNull();
        delegation.DomainEvents.Should().ContainSingle(e => e is FulfillmentDelegationRevokedEvent);
    }

    [Fact]
    public void Revoke_WhenNotActive_Fails()
    {
        FulfillmentDelegation delegation = CreateRequested();

        Result result = delegation.Revoke("reason");

        result.Error.Code.Should().Be(FulfillmentDelegationErrors.NotActive.Code);
    }

    [Fact]
    public void Revoke_WithoutReason_Fails()
    {
        FulfillmentDelegation delegation = CreateActive();

        Result result = delegation.Revoke("");

        result.Error.Code.Should().Be(FulfillmentDelegationErrors.RevokeReasonRequired.Code);
        delegation.Status.Should().Be(FulfillmentDelegationStatus.Active);
    }

    [Fact]
    public void Cancel_WhenRequested_TransitionsToCancelled()
    {
        FulfillmentDelegation delegation = CreateRequested();

        Result result = delegation.Cancel();

        result.IsSuccess.Should().BeTrue();
        delegation.Status.Should().Be(FulfillmentDelegationStatus.Cancelled);
        delegation.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public void Cancel_WhenActive_TransitionsToCancelled()
    {
        FulfillmentDelegation delegation = CreateActive();

        Result result = delegation.Cancel();

        result.IsSuccess.Should().BeTrue();
        delegation.Status.Should().Be(FulfillmentDelegationStatus.Cancelled);
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_IsIdempotent()
    {
        FulfillmentDelegation delegation = CreateRequested();
        delegation.Cancel();
        DateTime? firstCancelledAt = delegation.CancelledAt;

        Result result = delegation.Cancel();

        result.IsSuccess.Should().BeTrue();
        delegation.CancelledAt.Should().Be(firstCancelledAt);
    }

    [Fact]
    public void Cancel_WhenDeclined_Fails()
    {
        FulfillmentDelegation delegation = CreateRequested();
        delegation.Decline("reason");

        Result result = delegation.Cancel();

        result.Error.Code.Should().Be(FulfillmentDelegationErrors.InvalidStatusTransition.Code);
    }

    [Fact]
    public void ReRequest_FromDeclined_ResetsStateAndClearsReason()
    {
        FulfillmentDelegation delegation = CreateRequested(FulfillmentShippingMethod.Dropoff);
        delegation.Decline("No contract");

        Result result = delegation.ReRequest(FulfillmentShippingMethod.Pickup);

        result.IsSuccess.Should().BeTrue();
        delegation.Status.Should().Be(FulfillmentDelegationStatus.Requested);
        delegation.DefaultShippingMethod.Should().Be(FulfillmentShippingMethod.Pickup);
        delegation.DeclineReason.Should().BeNull();
        delegation.DeclinedAt.Should().BeNull();
        delegation.AcceptedAt.Should().BeNull();
    }

    [Fact]
    public void ReRequest_FromRevoked_ResetsState()
    {
        FulfillmentDelegation delegation = CreateActive();
        delegation.Revoke("Contract terminated");

        Result result = delegation.ReRequest(FulfillmentShippingMethod.Dropoff);

        result.IsSuccess.Should().BeTrue();
        delegation.Status.Should().Be(FulfillmentDelegationStatus.Requested);
        delegation.RevokeReason.Should().BeNull();
        delegation.RevokedAt.Should().BeNull();
    }

    [Fact]
    public void ReRequest_FromCancelled_ResetsState()
    {
        FulfillmentDelegation delegation = CreateRequested();
        delegation.Cancel();

        Result result = delegation.ReRequest(FulfillmentShippingMethod.Dropoff);

        result.IsSuccess.Should().BeTrue();
        delegation.Status.Should().Be(FulfillmentDelegationStatus.Requested);
        delegation.CancelledAt.Should().BeNull();
    }

    [Fact]
    public void ReRequest_WhenRequested_Fails()
    {
        FulfillmentDelegation delegation = CreateRequested();

        Result result = delegation.ReRequest(FulfillmentShippingMethod.Dropoff);

        result.Error.Code.Should().Be(FulfillmentDelegationErrors.AlreadyRequested.Code);
    }

    [Fact]
    public void ReRequest_WhenActive_Fails()
    {
        FulfillmentDelegation delegation = CreateActive();

        Result result = delegation.ReRequest(FulfillmentShippingMethod.Dropoff);

        result.Error.Code.Should().Be(FulfillmentDelegationErrors.AlreadyActive.Code);
    }

    [Fact]
    public void SetDefaultShippingMethod_WhenRequested_Updates()
    {
        FulfillmentDelegation delegation = CreateRequested(FulfillmentShippingMethod.Dropoff);

        Result result = delegation.SetDefaultShippingMethod(FulfillmentShippingMethod.Pickup);

        result.IsSuccess.Should().BeTrue();
        delegation.DefaultShippingMethod.Should().Be(FulfillmentShippingMethod.Pickup);
    }

    [Fact]
    public void SetDefaultShippingMethod_WhenActive_Updates()
    {
        FulfillmentDelegation delegation = CreateActive();

        Result result = delegation.SetDefaultShippingMethod(FulfillmentShippingMethod.Pickup);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void SetDefaultShippingMethod_WhenCancelled_Fails()
    {
        FulfillmentDelegation delegation = CreateRequested();
        delegation.Cancel();

        Result result = delegation.SetDefaultShippingMethod(FulfillmentShippingMethod.Pickup);

        result.Error.Code.Should().Be(FulfillmentDelegationErrors.InvalidStatusTransition.Code);
    }
}
