using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Billing;
using Wrapsfer.Application.Billing.Queries.GetStockReportBillingStatus;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Billing;

public class GetStockReportBillingStatusQueryHandlerTests
{
    private const string TenantId = "partner-acme";

    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<IFeatureEntitlementRepository> _entitlementRepository = new();
    private readonly GetStockReportBillingStatusQueryHandler _handler;

    public GetStockReportBillingStatusQueryHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns(TenantId);

        StripeOptions options = new()
        {
            Currency = "myr",
            StockReportMonthlyAmountMinor = 1000,
            BillingTimeZone = "Asia/Singapore"
        };

        _handler = new GetStockReportBillingStatusQueryHandler(
            _tenantContext.Object,
            _entitlementRepository.Object,
            new StockReportProrationCalculator(),
            Options.Create(options));
    }

    [Fact]
    public async Task Handle_WhenActive_ReturnsActiveWithLocalAccessDates()
    {
        // Window ends at 2026-07-01 00:00 Singapore (= 2026-06-30 16:00 UTC); access end date is 2026-06-30.
        FeatureEntitlement entitlement = FeatureEntitlement.CreatePending(
            TenantId, BillingFeature.StockReport,
            new DateTime(2026, 5, 31, 16, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 30, 16, 0, 0, DateTimeKind.Utc),
            267, "myr", "cs_active").Value;
        entitlement.Activate("pi", "in");
        _entitlementRepository.Setup(r => r.GetActiveAsync(
                TenantId, BillingFeature.StockReport, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entitlement);

        Result<StockReportBillingStatusResult> result =
            await _handler.Handle(new GetStockReportBillingStatusQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeTrue();
        result.Value.AccessStartDate.Should().Be(new DateOnly(2026, 6, 1));
        result.Value.AccessEndDate.Should().Be(new DateOnly(2026, 6, 30));
        result.Value.AmountMinorDue.Should().BeNull();
        result.Value.Currency.Should().Be("myr");
    }

    [Fact]
    public async Task Handle_WhenInactive_ReturnsProratedAmountDue()
    {
        _entitlementRepository.Setup(r => r.GetActiveAsync(
                TenantId, BillingFeature.StockReport, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FeatureEntitlement?)null);

        Result<StockReportBillingStatusResult> result =
            await _handler.Handle(new GetStockReportBillingStatusQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeFalse();
        result.Value.AccessStartDate.Should().NotBeNull();
        result.Value.AccessEndDate.Should().NotBeNull();
        result.Value.AccessEndDate.Should().BeOnOrAfter(result.Value.AccessStartDate!.Value);
        result.Value.AmountMinorDue.Should().NotBeNull();
        result.Value.AmountMinorDue!.Value.Should().BeInRange(1, 1000);
        result.Value.Currency.Should().Be("myr");
    }

    [Fact]
    public async Task Handle_ScopesLookupToCurrentTenant()
    {
        _entitlementRepository.Setup(r => r.GetActiveAsync(
                It.IsAny<string>(), It.IsAny<BillingFeature>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FeatureEntitlement?)null);

        await _handler.Handle(new GetStockReportBillingStatusQuery(), CancellationToken.None);

        // Tenant isolation: a partner can only ever read its own entitlement state.
        _entitlementRepository.Verify(r => r.GetActiveAsync(
            TenantId, BillingFeature.StockReport, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
