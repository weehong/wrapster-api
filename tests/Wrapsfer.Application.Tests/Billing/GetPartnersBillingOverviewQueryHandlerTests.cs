using Wrapsfer.Application.Billing;
using Wrapsfer.Application.Billing.Queries.GetPartnersBillingOverview;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Billing;

public class GetPartnersBillingOverviewQueryHandlerTests
{
    private const string OwnerRealm = "owner";
    private const string ActiveTenantId = "partner-acme";
    private const string InactiveTenantId = "partner-globex";

    private readonly Mock<IPartnerTenantRepository> _partnerRepository = new();
    private readonly Mock<IFeatureEntitlementRepository> _entitlementRepository = new();
    private readonly GetPartnersBillingOverviewQueryHandler _handler;

    public GetPartnersBillingOverviewQueryHandlerTests()
    {
        StripeOptions options = new()
        {
            Currency = "myr",
            StockReportMonthlyAmountMinor = 1000,
            BillingTimeZone = "Asia/Singapore"
        };

        _handler = new GetPartnersBillingOverviewQueryHandler(
            _partnerRepository.Object,
            _entitlementRepository.Object,
            new StockReportBillingStatusFactory(new StockReportProrationCalculator(), Options.Create(options)));
    }

    [Fact]
    public async Task Handle_ReturnsOneRowPerPartner_WithActiveAndInactiveStatus()
    {
        PartnerTenant activePartner = PartnerTenant.Create(ActiveTenantId, "Acme", OwnerRealm).Value;
        PartnerTenant inactivePartner = PartnerTenant.Create(InactiveTenantId, "Globex", OwnerRealm).Value;
        _partnerRepository.Setup(r => r.ListAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([activePartner, inactivePartner]);

        // Window ends 2026-07-01 00:00 Singapore (= 2026-06-30 16:00 UTC); access end date is 2026-06-30.
        FeatureEntitlement entitlement = FeatureEntitlement.CreatePending(
            ActiveTenantId, BillingFeature.StockReport,
            new DateTime(2026, 5, 31, 16, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 30, 16, 0, 0, DateTimeKind.Utc),
            267, "myr", "cs_active").Value;
        entitlement.Activate("pi", "in");
        _entitlementRepository.Setup(r => r.ListActiveAsync(
                BillingFeature.StockReport, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([entitlement]);

        Result<IReadOnlyList<PartnerBillingOverviewItem>> result =
            await _handler.Handle(new GetPartnersBillingOverviewQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);

        PartnerBillingOverviewItem active = result.Value.Single(i => i.TenantId == ActiveTenantId);
        active.DisplayName.Should().Be("Acme");
        active.Feature.Should().Be(BillingFeature.StockReport);
        active.FeatureKey.Should().Be("StockReport");
        active.IsActive.Should().BeTrue();
        active.AccessEndDate.Should().Be(new DateOnly(2026, 6, 30));
        active.AmountMinorDue.Should().BeNull();
        active.Currency.Should().Be("myr");

        PartnerBillingOverviewItem inactive = result.Value.Single(i => i.TenantId == InactiveTenantId);
        inactive.IsActive.Should().BeFalse();
        inactive.AmountMinorDue.Should().NotBeNull();
        inactive.AmountMinorDue!.Value.Should().BeInRange(1, 1000);
    }

    [Fact]
    public async Task Handle_WhenNoPartners_ReturnsEmpty()
    {
        _partnerRepository.Setup(r => r.ListAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _entitlementRepository.Setup(r => r.ListActiveAsync(
                BillingFeature.StockReport, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        Result<IReadOnlyList<PartnerBillingOverviewItem>> result =
            await _handler.Handle(new GetPartnersBillingOverviewQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
