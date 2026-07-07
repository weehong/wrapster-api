using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Domain.Tests.Entities;

public class FeatureEntitlementTests
{
    private const string TenantId = "partner-acme";
    private static readonly DateTime s_validFrom = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime s_validTo = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    private static FeatureEntitlement CreatePending() =>
        FeatureEntitlement.CreatePending(
            TenantId, BillingFeature.StockReport, s_validFrom, s_validTo, 267, "myr", "cs_test_1").Value;

    [Fact]
    public void CreatePending_SetsPendingStatusAndFields()
    {
        FeatureEntitlement entitlement = CreatePending();

        entitlement.Status.Should().Be(FeatureEntitlementStatus.PendingPayment);
        entitlement.TenantId.Should().Be(TenantId);
        entitlement.Feature.Should().Be(BillingFeature.StockReport);
        entitlement.AmountMinor.Should().Be(267);
        entitlement.Currency.Should().Be("myr");
        entitlement.StripeCheckoutSessionId.Should().Be("cs_test_1");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreatePending_WhenTenantIdMissing_Fails(string tenantId)
    {
        Result<FeatureEntitlement> result = FeatureEntitlement.CreatePending(
            tenantId, BillingFeature.StockReport, s_validFrom, s_validTo, 267, "myr", "cs_test_1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BillingErrors.InvalidTenantId);
    }

    [Fact]
    public void CreatePending_WhenValidToBeforeValidFrom_Fails()
    {
        Result<FeatureEntitlement> result = FeatureEntitlement.CreatePending(
            TenantId, BillingFeature.StockReport, s_validTo, s_validFrom, 267, "myr", "cs_test_1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BillingErrors.InvalidValidityRange);
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void CreatePending_WhenValidityTimestampsNotUtc_Fails(DateTimeKind kind)
    {
        DateTime validFrom = DateTime.SpecifyKind(s_validFrom, kind);
        DateTime validTo = DateTime.SpecifyKind(s_validTo, kind);

        Result<FeatureEntitlement> result = FeatureEntitlement.CreatePending(
            TenantId, BillingFeature.StockReport, validFrom, validTo, 267, "myr", "cs_test_1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BillingErrors.NonUtcValidityTimestamp);
    }

    [Fact]
    public void CreatePending_WhenAmountNotPositive_Fails()
    {
        Result<FeatureEntitlement> result = FeatureEntitlement.CreatePending(
            TenantId, BillingFeature.StockReport, s_validFrom, s_validTo, 0, "myr", "cs_test_1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BillingErrors.InvalidAmount);
    }

    [Fact]
    public void IsActiveAt_WhenPending_IsFalseEvenWithinWindow()
    {
        FeatureEntitlement entitlement = CreatePending();

        entitlement.IsActiveAt(new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc)).Should().BeFalse();
    }

    [Fact]
    public void IsActiveAt_WhenActiveAndWithinWindow_IsTrue()
    {
        FeatureEntitlement entitlement = CreatePending();
        entitlement.Activate("pi_1", "in_1");

        entitlement.IsActiveAt(new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc)).Should().BeTrue();
    }

    [Fact]
    public void IsActiveAt_OnLowerBoundIsInclusive_OnUpperBoundIsExclusive()
    {
        FeatureEntitlement entitlement = CreatePending();
        entitlement.Activate(null, null);

        entitlement.IsActiveAt(s_validFrom).Should().BeTrue();
        entitlement.IsActiveAt(s_validTo).Should().BeFalse();
    }

    [Fact]
    public void IsActiveAt_WhenExpired_IsFalse()
    {
        FeatureEntitlement entitlement = CreatePending();
        entitlement.Activate("pi_1", "in_1");

        entitlement.IsActiveAt(new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc)).Should().BeFalse();
    }

    [Fact]
    public void Activate_SetsActiveAndStripeReferences()
    {
        FeatureEntitlement entitlement = CreatePending();

        Result result = entitlement.Activate("pi_1", "in_1");

        result.IsSuccess.Should().BeTrue();
        entitlement.Status.Should().Be(FeatureEntitlementStatus.Active);
        entitlement.StripePaymentIntentId.Should().Be("pi_1");
        entitlement.StripeInvoiceId.Should().Be("in_1");
    }

    [Fact]
    public void Activate_WhenAlreadyActive_IsIdempotentAndKeepsOriginalReferences()
    {
        FeatureEntitlement entitlement = CreatePending();
        entitlement.Activate("pi_1", "in_1");

        Result result = entitlement.Activate("pi_2", "in_2");

        result.IsSuccess.Should().BeTrue();
        entitlement.Status.Should().Be(FeatureEntitlementStatus.Active);
        entitlement.StripePaymentIntentId.Should().Be("pi_1");
        entitlement.StripeInvoiceId.Should().Be("in_1");
    }
}
