using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Billing;
using Wrapsfer.Application.Billing.Abstractions;
using Wrapsfer.Application.Billing.Queries.ListBillingHistory;
using Wrapsfer.Application.Billing.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Billing;

public class ListBillingHistoryQueryHandlerTests
{
    private const string TenantId = "partner-acme";
    private const string CustomerId = "cus_test";

    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<IPartnerBillingCustomerRepository> _billingCustomerRepository = new();
    private readonly Mock<IFeatureEntitlementRepository> _entitlementRepository = new();
    private readonly Mock<IStripeBillingGateway> _gateway = new();
    private readonly ListBillingHistoryQueryHandler _handler;

    public ListBillingHistoryQueryHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns(TenantId);

        _handler = new ListBillingHistoryQueryHandler(
            _tenantContext.Object,
            _billingCustomerRepository.Object,
            _entitlementRepository.Object,
            _gateway.Object,
            Options.Create(new StripeOptions
            {
                BillingTimeZone = "Asia/Singapore"
            }));
    }

    [Fact]
    public async Task Handle_WhenCustomerDoesNotExist_ReturnsEmptyHistory()
    {
        _billingCustomerRepository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PartnerBillingCustomer?)null);

        Result<IReadOnlyList<BillingHistoryItemResponse>> result =
            await _handler.Handle(new ListBillingHistoryQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
        _gateway.Verify(g => g.ListBillingHistoryAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _entitlementRepository.Verify(r => r.ListByTenantAndFeatureAsync(
            It.IsAny<string>(), It.IsAny<BillingFeature>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCustomerExists_ReturnsStripeBillingHistory()
    {
        DateTime occurredAtUtc = new(2026, 6, 24, 4, 42, 51, DateTimeKind.Utc);
        PartnerBillingCustomer customer = PartnerBillingCustomer.Create(TenantId, CustomerId).Value;
        StripeBillingHistoryItem item = new(
            "re_123",
            "Refund",
            "succeeded",
            occurredAtUtc,
            233,
            "myr",
            "Refund for OSUSHWRF-0001",
            "https://invoice.stripe.test/i/test",
            "https://invoice.stripe.test/i/test/pdf",
            "in_123",
            "pi_123",
            "re_123");
        FeatureEntitlement entitlement = FeatureEntitlement.CreatePending(
            TenantId,
            BillingFeature.StockReport,
            new DateTime(2026, 5, 31, 16, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 30, 16, 0, 0, DateTimeKind.Utc),
            233,
            "myr",
            "cs_123").Value;
        entitlement.Activate("pi_123", "in_123");

        _billingCustomerRepository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);
        _gateway.Setup(g => g.ListBillingHistoryAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([item]);
        _entitlementRepository.Setup(r => r.ListByTenantAndFeatureAsync(
                TenantId, BillingFeature.StockReport, It.IsAny<CancellationToken>()))
            .ReturnsAsync([entitlement]);

        Result<IReadOnlyList<BillingHistoryItemResponse>> result =
            await _handler.Handle(new ListBillingHistoryQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new BillingHistoryItemResponse(
                item.Id,
                item.Type,
                item.Status,
                item.OccurredAtUtc,
                item.AmountMinor,
                item.Currency,
                item.Description,
                new DateOnly(2026, 6, 24),
                new DateOnly(2026, 6, 30),
                item.HostedInvoiceUrl,
                item.InvoicePdfUrl,
                item.StripeInvoiceId,
                item.StripePaymentIntentId,
                item.StripeRefundId));
        _gateway.Verify(g => g.ListBillingHistoryAsync(CustomerId, It.IsAny<CancellationToken>()), Times.Once);
        _entitlementRepository.Verify(r => r.ListByTenantAndFeatureAsync(
            TenantId, BillingFeature.StockReport, It.IsAny<CancellationToken>()), Times.Once);
    }
}
