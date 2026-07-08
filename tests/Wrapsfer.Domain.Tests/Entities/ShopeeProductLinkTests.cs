using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Domain.Tests.Entities;

public class ShopeeProductLinkTests
{
    private const string TenantId = "partner-acme";
    private static readonly Guid s_productId = Guid.NewGuid();
    private static readonly DateTime s_now = new(2026, 7, 8, 0, 0, 0, DateTimeKind.Utc);

    private static ShopeeProductLink CreateLink() =>
        ShopeeProductLink.Create(
            TenantId,
            s_productId,
            1001,
            0,
            "Item",
            null,
            "SKU",
            "admin@acme").Value;

    [Fact]
    public void Create_WhenValid_SetsFields()
    {
        ShopeeProductLink link = CreateLink();

        link.TenantId.Should().Be(TenantId);
        link.ProductId.Should().Be(s_productId);
        link.ShopeeItemId.Should().Be(1001);
        link.ShopeeModelId.Should().Be(0);
        link.ShopeeItemName.Should().Be("Item");
        link.ShopeeItemSku.Should().Be("SKU");
        link.LinkedBy.Should().Be("admin@acme");
    }

    [Theory]
    [InlineData("", 1001, 0, "ShopeeProductLink.InvalidTenantId")]
    [InlineData("partner-acme", 0, 0, "ShopeeProductLink.InvalidItemId")]
    [InlineData("partner-acme", 1001, -1, "ShopeeProductLink.InvalidModelId")]
    public void Create_WhenInvalid_Fails(string tenantId, long itemId, long modelId, string errorCode)
    {
        Result<ShopeeProductLink> result = ShopeeProductLink.Create(
            tenantId,
            s_productId,
            itemId,
            modelId,
            null,
            null,
            null,
            null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(errorCode);
    }

    [Fact]
    public void MarkSynced_ResetsFailureState()
    {
        ShopeeProductLink link = CreateLink();
        link.MarkSyncFailed("temporary failure", s_now);

        link.MarkSynced(12, s_now.AddMinutes(1));

        link.LastSyncedQuantity.Should().Be(12);
        link.LastSyncedAt.Should().Be(s_now.AddMinutes(1));
        link.LastSyncAttemptedAt.Should().Be(s_now.AddMinutes(1));
        link.LastSyncError.Should().BeNull();
        link.SyncFailureCount.Should().Be(0);
        link.NextSyncEligibleAt.Should().BeNull();
    }

    [Fact]
    public void MarkSyncFailed_IncrementsBackoffUntilSixHourCap()
    {
        ShopeeProductLink link = CreateLink();

        link.MarkSyncFailed("failure", s_now);
        link.NextSyncEligibleAt.Should().Be(s_now.AddMinutes(5));

        link.MarkSyncFailed("failure", s_now);
        link.NextSyncEligibleAt.Should().Be(s_now.AddMinutes(10));

        link.MarkSyncFailed("failure", s_now);
        link.NextSyncEligibleAt.Should().Be(s_now.AddMinutes(20));

        for (int i = 0; i < 10; i++)
        {
            link.MarkSyncFailed("failure", s_now);
        }

        link.NextSyncEligibleAt.Should().Be(s_now.AddHours(6));
    }

    [Fact]
    public void SnapshotAndErrorFields_AreTruncated()
    {
        string longText = new('x', 600);
        ShopeeProductLink link = ShopeeProductLink.Create(
            TenantId,
            s_productId,
            1001,
            2002,
            longText,
            longText,
            longText,
            null).Value;

        link.MarkSyncFailed(longText, s_now);

        link.ShopeeItemName.Should().HaveLength(256);
        link.ShopeeModelName.Should().HaveLength(256);
        link.ShopeeItemSku.Should().HaveLength(128);
        link.LastSyncError.Should().HaveLength(512);
    }
}
