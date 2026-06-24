using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Domain.Tests.Entities;

public class PurchaseOrderTests
{
    private const string TenantId = "test-tenant";

    private static Result<PurchaseOrder> CreateValid(string poNumber = "PO-001", int quantity = 5) =>
        PurchaseOrder.Create(TenantId, poNumber, Guid.NewGuid(), "BC-1", "Test Product", quantity);

    [Fact]
    public void Create_WithValidInputs_ReturnsPendingAndRaisesCreatedEvent()
    {
        Result<PurchaseOrder> result = CreateValid();

        result.IsSuccess.Should().BeTrue();
        PurchaseOrder purchaseOrder = result.Value;
        purchaseOrder.Status.Should().Be(PurchaseOrderStatus.Pending);
        purchaseOrder.PoNumber.Should().Be("PO-001");
        purchaseOrder.Quantity.Should().Be(5);
        purchaseOrder.ReceivedAt.Should().BeNull();
        purchaseOrder.RejectedAt.Should().BeNull();
        purchaseOrder.DomainEvents.Should().HaveCount(1);
    }

    [Fact]
    public void Create_WithBlankTenantId_Fails()
    {
        Result<PurchaseOrder> result =
            PurchaseOrder.Create("  ", "PO-1", Guid.NewGuid(), "BC-1", "Test Product", 1);

        result.Error.Code.Should().Be(PurchaseOrderErrors.InvalidTenantId.Code);
    }

    [Fact]
    public void Create_WithBlankPoNumber_Fails()
    {
        Result<PurchaseOrder> result =
            PurchaseOrder.Create(TenantId, "", Guid.NewGuid(), "BC-1", "Test Product", 1);

        result.Error.Code.Should().Be(PurchaseOrderErrors.InvalidPoNumber.Code);
    }

    [Fact]
    public void Create_WithPoNumberOver100Chars_Fails()
    {
        Result<PurchaseOrder> result =
            PurchaseOrder.Create(TenantId, new string('A', 101), Guid.NewGuid(), "BC-1", "Test Product", 1);

        result.Error.Code.Should().Be(PurchaseOrderErrors.PoNumberTooLong.Code);
    }

    [Fact]
    public void Create_WithEmptyProductId_Fails()
    {
        Result<PurchaseOrder> result =
            PurchaseOrder.Create(TenantId, "PO-1", Guid.Empty, "BC-1", "Test Product", 1);

        result.Error.Code.Should().Be(PurchaseOrderErrors.InvalidProductId.Code);
    }

    [Fact]
    public void Create_WithNonPositiveQuantity_Fails()
    {
        Result<PurchaseOrder> result = CreateValid(quantity: 0);

        result.Error.Code.Should().Be(PurchaseOrderErrors.InvalidQuantity.Code);
    }

    [Fact]
    public void Receive_WhenPending_TransitionsToReceivedAndRaisesEvent()
    {
        PurchaseOrder purchaseOrder = CreateValid().Value;
        purchaseOrder.ClearDomainEvents();

        Result result = purchaseOrder.Receive();

        result.IsSuccess.Should().BeTrue();
        purchaseOrder.Status.Should().Be(PurchaseOrderStatus.Received);
        purchaseOrder.ReceivedAt.Should().NotBeNull();
        purchaseOrder.DomainEvents.Should().ContainSingle();
    }

    [Fact]
    public void Receive_WhenNotPending_Fails()
    {
        PurchaseOrder purchaseOrder = CreateValid().Value;
        purchaseOrder.Receive();

        Result result = purchaseOrder.Receive();

        result.Error.Code.Should().Be(PurchaseOrderErrors.InvalidStatusTransition.Code);
    }

    [Fact]
    public void Reject_WhenPending_TransitionsToRejectedAndRaisesEvent()
    {
        PurchaseOrder purchaseOrder = CreateValid().Value;
        purchaseOrder.ClearDomainEvents();

        Result result = purchaseOrder.Reject("Out of stock at supplier");

        result.IsSuccess.Should().BeTrue();
        purchaseOrder.Status.Should().Be(PurchaseOrderStatus.Rejected);
        purchaseOrder.RejectionReason.Should().Be("Out of stock at supplier");
        purchaseOrder.RejectedAt.Should().NotBeNull();
        purchaseOrder.DomainEvents.Should().ContainSingle();
    }

    [Fact]
    public void Reject_WithBlankReason_Fails()
    {
        PurchaseOrder purchaseOrder = CreateValid().Value;

        Result result = purchaseOrder.Reject("   ");

        result.Error.Code.Should().Be(PurchaseOrderErrors.RejectionReasonRequired.Code);
        purchaseOrder.Status.Should().Be(PurchaseOrderStatus.Pending);
    }

    [Fact]
    public void Reject_WhenAlreadyReceived_Fails()
    {
        PurchaseOrder purchaseOrder = CreateValid().Value;
        purchaseOrder.Receive();

        Result result = purchaseOrder.Reject("too late");

        result.Error.Code.Should().Be(PurchaseOrderErrors.InvalidStatusTransition.Code);
    }

    [Fact]
    public void Update_WhenPending_ChangesPoNumberAndQuantity()
    {
        PurchaseOrder purchaseOrder = CreateValid().Value;

        Result result = purchaseOrder.Update("PO-EDITED", 42);

        result.IsSuccess.Should().BeTrue();
        purchaseOrder.PoNumber.Should().Be("PO-EDITED");
        purchaseOrder.Quantity.Should().Be(42);
    }

    [Fact]
    public void Update_WithBlankPoNumber_Fails()
    {
        PurchaseOrder purchaseOrder = CreateValid().Value;

        Result result = purchaseOrder.Update("  ", 5);

        result.Error.Code.Should().Be(PurchaseOrderErrors.InvalidPoNumber.Code);
    }

    [Fact]
    public void Update_WithNonPositiveQuantity_Fails()
    {
        PurchaseOrder purchaseOrder = CreateValid().Value;

        Result result = purchaseOrder.Update("PO-1", 0);

        result.Error.Code.Should().Be(PurchaseOrderErrors.InvalidQuantity.Code);
    }

    [Fact]
    public void Update_WhenReceived_Fails()
    {
        PurchaseOrder purchaseOrder = CreateValid().Value;
        purchaseOrder.Receive();

        Result result = purchaseOrder.Update("PO-EDITED", 9);

        result.Error.Code.Should().Be(PurchaseOrderErrors.NotPendingForEdit.Code);
        purchaseOrder.PoNumber.Should().Be("PO-001");
    }

    [Fact]
    public void Delete_WhenPending_MarksDeleted()
    {
        PurchaseOrder purchaseOrder = CreateValid().Value;

        Result result = purchaseOrder.Delete();

        result.IsSuccess.Should().BeTrue();
        purchaseOrder.IsDeleted.Should().BeTrue();
        purchaseOrder.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Delete_WhenRejected_Fails()
    {
        PurchaseOrder purchaseOrder = CreateValid().Value;
        purchaseOrder.Reject("supplier closed");

        Result result = purchaseOrder.Delete();

        result.Error.Code.Should().Be(PurchaseOrderErrors.NotPendingForDelete.Code);
        purchaseOrder.IsDeleted.Should().BeFalse();
    }
}
