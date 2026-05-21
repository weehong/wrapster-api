using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Domain.Tests.Entities;

public class WaybillTests
{
    private const string TenantId = "test-tenant";
    private static readonly DateOnly TestDate = new(2026, 4, 13);

    [Fact]
    public void Create_WithValidInputs_ReturnsSuccessAndRaisesCreatedEvent()
    {
        Result<Waybill> result = Waybill.Create(TenantId, TestDate, "WB-001");

        result.IsSuccess.Should().BeTrue();
        Waybill waybill = result.Value;
        waybill.Status.Should().Be(WaybillStatus.Draft);
        waybill.WaybillNumber.Should().Be("WB-001");
        waybill.PackagingDate.Should().Be(TestDate);
        waybill.Items.Should().BeEmpty();
        waybill.DomainEvents.Should().HaveCount(1);
    }

    [Fact]
    public void Create_WithBlankTenantId_Fails()
    {
        Result<Waybill> result = Waybill.Create("  ", TestDate, "WB-001");

        result.Error.Code.Should().Be(WaybillErrors.InvalidTenantId.Code);
    }

    [Fact]
    public void Create_WithBlankWaybillNumber_Fails()
    {
        Result<Waybill> result = Waybill.Create(TenantId, TestDate, "");

        result.Error.Code.Should().Be(WaybillErrors.InvalidWaybillNumber.Code);
    }

    [Fact]
    public void Create_WithWaybillNumberOver100Chars_Fails()
    {
        Result<Waybill> result = Waybill.Create(TenantId, TestDate, new string('A', 101));

        result.Error.Code.Should().Be(WaybillErrors.WaybillNumberTooLong.Code);
    }

    [Fact]
    public void AddOrIncrementItem_NewProduct_AppendsNewItem()
    {
        Waybill waybill = Waybill.Create(TenantId, TestDate, "WB-001").Value;
        Guid productId = Guid.NewGuid();

        Result<WaybillItem> result = waybill.AddOrIncrementItem(productId, "BC-1", 2);

        result.IsSuccess.Should().BeTrue();
        waybill.Items.Should().HaveCount(1);
        waybill.Items.First().Quantity.Should().Be(2);
    }

    [Fact]
    public void AddOrIncrementItem_ExistingProduct_MergesQuantity()
    {
        Waybill waybill = Waybill.Create(TenantId, TestDate, "WB-001").Value;
        Guid productId = Guid.NewGuid();
        waybill.AddOrIncrementItem(productId, "BC-1", 2);

        Result<WaybillItem> result = waybill.AddOrIncrementItem(productId, "BC-1", 3);

        result.IsSuccess.Should().BeTrue();
        waybill.Items.Should().HaveCount(1);
        waybill.Items.First().Quantity.Should().Be(5);
    }

    [Fact]
    public void AddOrIncrementItem_AfterPacked_Fails()
    {
        Waybill waybill = CreateWaybillWithItem(out _);
        waybill.MarkPacked();

        Result<WaybillItem> result = waybill.AddOrIncrementItem(Guid.NewGuid(), "BC-2", 1);

        result.Error.Code.Should().Be(WaybillErrors.CannotEditNonDraft.Code);
    }

    [Fact]
    public void UpdateItemQuantity_IncreasesQuantity_ReturnsPositiveDelta()
    {
        Waybill waybill = CreateWaybillWithItem(out WaybillItem item);

        Result<QuantityChange> result = waybill.UpdateItemQuantity(item.Id, 5);

        result.IsSuccess.Should().BeTrue();
        result.Value.Delta.Should().Be(3);
        result.Value.ProductId.Should().Be(item.ProductId);
        item.Quantity.Should().Be(5);
    }

    [Fact]
    public void UpdateItemQuantity_DecreasesQuantity_ReturnsNegativeDelta()
    {
        Waybill waybill = CreateWaybillWithItem(out WaybillItem item);

        Result<QuantityChange> result = waybill.UpdateItemQuantity(item.Id, 1);

        result.IsSuccess.Should().BeTrue();
        result.Value.Delta.Should().Be(-1);
        item.Quantity.Should().Be(1);
    }

    [Fact]
    public void UpdateItemQuantity_NonExistingItem_ReturnsItemNotFound()
    {
        Waybill waybill = CreateWaybillWithItem(out _);

        Result<QuantityChange> result = waybill.UpdateItemQuantity(Guid.NewGuid(), 1);

        result.Error.Code.Should().Be(WaybillErrors.ItemNotFound.Code);
    }

    [Fact]
    public void RemoveItem_ReturnsQuantityForStockRelease()
    {
        Waybill waybill = CreateWaybillWithItem(out WaybillItem item);

        Result<WaybillItemRemoval> result = waybill.RemoveItem(item.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.ProductId.Should().Be(item.ProductId);
        result.Value.Quantity.Should().Be(2);
        waybill.Items.Should().BeEmpty();
    }

    [Fact]
    public void MarkPacked_FromDraftWithItems_Succeeds()
    {
        Waybill waybill = CreateWaybillWithItem(out _);

        Result result = waybill.MarkPacked();

        result.IsSuccess.Should().BeTrue();
        waybill.Status.Should().Be(WaybillStatus.Packed);
        waybill.PackedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkPacked_WithoutItems_ReturnsEmptyWaybill()
    {
        Waybill waybill = Waybill.Create(TenantId, TestDate, "WB-001").Value;

        Result result = waybill.MarkPacked();

        result.Error.Code.Should().Be(WaybillErrors.EmptyWaybill.Code);
    }

    [Fact]
    public void MarkPacked_WhenAlreadyPacked_ReturnsInvalidStatusTransition()
    {
        Waybill waybill = CreateWaybillWithItem(out _);
        waybill.MarkPacked();

        Result result = waybill.MarkPacked();

        result.Error.Code.Should().Be(WaybillErrors.InvalidStatusTransition.Code);
    }

    [Fact]
    public void MarkHandedOff_FromPackedByCreator_Succeeds()
    {
        Waybill waybill = CreateWaybillWithItem(out _);
        waybill.SetCreatedBy("user-1");
        waybill.MarkPacked();

        Result result = waybill.MarkHandedOff("user-1");

        result.IsSuccess.Should().BeTrue();
        waybill.Status.Should().Be(WaybillStatus.HandedOff);
        waybill.HandedOffAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkHandedOff_ByNonCreator_ReturnsNotWaybillCreator()
    {
        Waybill waybill = CreateWaybillWithItem(out _);
        waybill.SetCreatedBy("user-1");
        waybill.MarkPacked();

        Result result = waybill.MarkHandedOff("user-2");

        result.Error.Code.Should().Be(WaybillErrors.NotWaybillCreator.Code);
    }

    [Fact]
    public void MarkHandedOff_FromDraft_ReturnsInvalidStatusTransition()
    {
        Waybill waybill = CreateWaybillWithItem(out _);
        waybill.SetCreatedBy("user-1");

        Result result = waybill.MarkHandedOff("user-1");

        result.Error.Code.Should().Be(WaybillErrors.InvalidStatusTransition.Code);
    }

    [Fact]
    public void Cancel_FromDraft_Succeeds()
    {
        Waybill waybill = CreateWaybillWithItem(out _);

        Result result = waybill.Cancel("damaged packaging");

        result.IsSuccess.Should().BeTrue();
        waybill.Status.Should().Be(WaybillStatus.Cancelled);
        waybill.CancellationReason.Should().Be("damaged packaging");
    }

    [Fact]
    public void Cancel_FromPacked_Succeeds()
    {
        Waybill waybill = CreateWaybillWithItem(out _);
        waybill.MarkPacked();

        Result result = waybill.Cancel("customer change of mind");

        result.IsSuccess.Should().BeTrue();
        waybill.Status.Should().Be(WaybillStatus.Cancelled);
    }

    [Fact]
    public void Cancel_AfterHandedOff_ReturnsCannotCancelAfterHandedOff()
    {
        Waybill waybill = CreateWaybillWithItem(out _);
        waybill.SetCreatedBy("user-1");
        waybill.MarkPacked();
        waybill.MarkHandedOff("user-1");

        Result result = waybill.Cancel("too late");

        result.Error.Code.Should().Be(WaybillErrors.CannotCancelAfterHandedOff.Code);
    }

    [Fact]
    public void Cancel_WithEmptyReason_Fails()
    {
        Waybill waybill = CreateWaybillWithItem(out _);

        Result result = waybill.Cancel("  ");

        result.Error.Code.Should().Be(WaybillErrors.CancellationReasonRequired.Code);
    }

    [Fact]
    public void UpdateWaybillNumber_AfterPacked_ReturnsCannotEditNonDraft()
    {
        Waybill waybill = CreateWaybillWithItem(out _);
        waybill.MarkPacked();

        Result result = waybill.UpdateWaybillNumber("WB-002");

        result.Error.Code.Should().Be(WaybillErrors.CannotEditNonDraft.Code);
    }

    [Fact]
    public void Items_ShouldBeReadOnlyCollection()
    {
        Waybill waybill = CreateWaybillWithItem(out _);

        waybill.Items.Should().BeAssignableTo<IReadOnlyCollection<WaybillItem>>();
    }

    [Fact]
    public void UpdatePackagingDate_OnDraft_Succeeds()
    {
        Waybill waybill = Waybill.Create(TenantId, TestDate, "WB-001").Value;
        DateOnly newDate = new(2026, 6, 1);

        Result result = waybill.UpdatePackagingDate(newDate);

        result.IsSuccess.Should().BeTrue();
        waybill.PackagingDate.Should().Be(newDate);
    }

    [Fact]
    public void UpdatePackagingDate_AfterPacked_Fails()
    {
        Waybill waybill = CreateWaybillWithItem(out _);
        waybill.MarkPacked();

        Result result = waybill.UpdatePackagingDate(new DateOnly(2026, 6, 1));

        result.Error.Code.Should().Be(WaybillErrors.CannotEditNonDraft.Code);
    }

    [Fact]
    public void ReplaceItems_AddsNewItemAndReturnsPositiveDelta()
    {
        Waybill waybill = Waybill.Create(TenantId, TestDate, "WB-001").Value;
        Guid productId = Guid.NewGuid();

        Result<IReadOnlyList<QuantityChange>> result = waybill.ReplaceItems(
            new List<(Guid, string, int)> { (productId, "BC-1", 4) });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].ProductId.Should().Be(productId);
        result.Value[0].Delta.Should().Be(4);
        waybill.Items.Should().ContainSingle()
            .Which.Quantity.Should().Be(4);
    }

    [Fact]
    public void ReplaceItems_IncreasesExistingQuantityWithPositiveDelta()
    {
        Waybill waybill = CreateWaybillWithItem(out WaybillItem item);

        Result<IReadOnlyList<QuantityChange>> result = waybill.ReplaceItems(
            new List<(Guid, string, int)> { (item.ProductId, item.ProductBarcode, 5) });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle()
            .Which.Delta.Should().Be(3);
        item.Quantity.Should().Be(5);
    }

    [Fact]
    public void ReplaceItems_DecreasesExistingQuantityWithNegativeDelta()
    {
        Waybill waybill = CreateWaybillWithItem(out WaybillItem item);

        Result<IReadOnlyList<QuantityChange>> result = waybill.ReplaceItems(
            new List<(Guid, string, int)> { (item.ProductId, item.ProductBarcode, 1) });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle()
            .Which.Delta.Should().Be(-1);
        item.Quantity.Should().Be(1);
    }

    [Fact]
    public void ReplaceItems_RemovesOmittedItemsAndReturnsNegativeDelta()
    {
        Waybill waybill = CreateWaybillWithItem(out WaybillItem item);

        Result<IReadOnlyList<QuantityChange>> result =
            waybill.ReplaceItems(new List<(Guid, string, int)>());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].ProductId.Should().Be(item.ProductId);
        result.Value[0].Delta.Should().Be(-item.Quantity);
        waybill.Items.Should().BeEmpty();
    }

    [Fact]
    public void ReplaceItems_NoChange_ReturnsEmptyChanges()
    {
        Waybill waybill = CreateWaybillWithItem(out WaybillItem item);

        Result<IReadOnlyList<QuantityChange>> result = waybill.ReplaceItems(
            new List<(Guid, string, int)> { (item.ProductId, item.ProductBarcode, item.Quantity) });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public void ReplaceItems_SumsDuplicateProductIds()
    {
        Waybill waybill = Waybill.Create(TenantId, TestDate, "WB-001").Value;
        Guid productId = Guid.NewGuid();

        Result<IReadOnlyList<QuantityChange>> result = waybill.ReplaceItems(
            new List<(Guid, string, int)>
            {
                (productId, "BC-1", 2),
                (productId, "BC-1", 3)
            });

        result.IsSuccess.Should().BeTrue();
        waybill.Items.Should().ContainSingle()
            .Which.Quantity.Should().Be(5);
        result.Value.Should().ContainSingle()
            .Which.Delta.Should().Be(5);
    }

    [Fact]
    public void ReplaceItems_AfterPacked_Fails()
    {
        Waybill waybill = CreateWaybillWithItem(out _);
        waybill.MarkPacked();

        Result<IReadOnlyList<QuantityChange>> result = waybill.ReplaceItems(
            new List<(Guid, string, int)> { (Guid.NewGuid(), "BC-2", 1) });

        result.Error.Code.Should().Be(WaybillErrors.CannotEditNonDraft.Code);
    }

    [Fact]
    public void ReplaceItems_InvalidQuantity_Fails()
    {
        Waybill waybill = Waybill.Create(TenantId, TestDate, "WB-001").Value;

        Result<IReadOnlyList<QuantityChange>> result = waybill.ReplaceItems(
            new List<(Guid, string, int)> { (Guid.NewGuid(), "BC-1", 0) });

        result.Error.Code.Should().Be(WaybillErrors.InvalidQuantity.Code);
    }

    private static Waybill CreateWaybillWithItem(out WaybillItem item)
    {
        Waybill waybill = Waybill.Create(TenantId, TestDate, "WB-001").Value;
        Result<WaybillItem> addResult = waybill.AddOrIncrementItem(Guid.NewGuid(), "BC-1", 2);
        item = addResult.Value;
        return waybill;
    }
}
