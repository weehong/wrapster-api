using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Billing;
using Wrapsfer.Application.Billing.Abstractions;
using Wrapsfer.Application.Billing.Commands.CreateStockReportCheckout;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Billing;

public class CreateStockReportCheckoutCommandHandlerTests
{
    private const string TenantId = "partner-acme";
    private const string CustomerId = "cus_test";
    private const string SessionId = "cs_test_123";
    private const string CheckoutUrl = "https://checkout.stripe.com/c/pay/cs_test_123";

    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<IStripeBillingGateway> _gateway = new();
    private readonly Mock<IPartnerBillingCustomerRepository> _billingCustomerRepository = new();
    private readonly Mock<IFeatureEntitlementRepository> _entitlementRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly CreateStockReportCheckoutCommandHandler _handler;

    public CreateStockReportCheckoutCommandHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns(TenantId);
        _tenantContext.Setup(x => x.Email).Returns("ops@acme.test");
        _tenantContext.Setup(x => x.DisplayName).Returns("Acme Ops");

        _gateway.Setup(g => g.CreateCheckoutSessionAsync(
                It.IsAny<StripeCheckoutSessionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StripeCheckoutSessionResult(SessionId, CheckoutUrl));

        StripeCustomerProvisioner provisioner = new(
            _billingCustomerRepository.Object, _gateway.Object, _tenantContext.Object);

        StripeOptions options = new()
        {
            Currency = "myr",
            StockReportMonthlyAmountMinor = 1000,
            BillingTimeZone = "Asia/Singapore",
            FrontendBaseUrl = "https://app.example.com"
        };

        _handler = new CreateStockReportCheckoutCommandHandler(
            _tenantContext.Object,
            new StockReportProrationCalculator(),
            provisioner,
            _gateway.Object,
            _entitlementRepository.Object,
            _unitOfWork.Object,
            Options.Create(options));
    }

    [Fact]
    public async Task Handle_WhenAlreadyActive_ReturnsConflictAndDoesNotChargeAgain()
    {
        FeatureEntitlement active = FeatureEntitlement.CreatePending(
            TenantId, BillingFeature.StockReport,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(10), 267, "myr", "cs_existing").Value;
        active.Activate("pi", "in");
        _entitlementRepository.Setup(r => r.GetActiveAsync(
                TenantId, BillingFeature.StockReport, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(active);

        Result<CreateStockReportCheckoutResult> result =
            await _handler.Handle(new CreateStockReportCheckoutCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BillingErrors.StockReportAlreadyActive);
        _gateway.Verify(g => g.CreateCheckoutSessionAsync(
            It.IsAny<StripeCheckoutSessionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNewCustomer_CreatesCustomerEntitlementAndReturnsUrl()
    {
        _entitlementRepository.Setup(r => r.GetActiveAsync(
                TenantId, BillingFeature.StockReport, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FeatureEntitlement?)null);
        _billingCustomerRepository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PartnerBillingCustomer?)null);
        _gateway.Setup(g => g.CreateCustomerAsync(
                TenantId, "ops@acme.test", "Acme Ops", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CustomerId);

        StripeCheckoutSessionRequest? capturedRequest = null;
        _gateway.Setup(g => g.CreateCheckoutSessionAsync(
                It.IsAny<StripeCheckoutSessionRequest>(), It.IsAny<CancellationToken>()))
            .Callback<StripeCheckoutSessionRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new StripeCheckoutSessionResult(SessionId, CheckoutUrl));

        FeatureEntitlement? addedEntitlement = null;
        _entitlementRepository.Setup(r => r.Add(It.IsAny<FeatureEntitlement>()))
            .Callback<FeatureEntitlement>(e => addedEntitlement = e);

        Result<CreateStockReportCheckoutResult> result =
            await _handler.Handle(new CreateStockReportCheckoutCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CheckoutUrl.Should().Be(CheckoutUrl);

        _billingCustomerRepository.Verify(r => r.Add(It.Is<PartnerBillingCustomer>(
            c => c.TenantId == TenantId && c.StripeCustomerId == CustomerId)), Times.Once);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.CustomerId.Should().Be(CustomerId);
        capturedRequest.Currency.Should().Be("myr");
        capturedRequest.AmountMinor.Should().BeGreaterThan(0);
        capturedRequest.SuccessUrl.Should().StartWith("https://app.example.com/");
        capturedRequest.Metadata[BillingMetadataKeys.TenantId].Should().Be(TenantId);
        capturedRequest.Metadata[BillingMetadataKeys.Feature].Should().Be(nameof(BillingFeature.StockReport));

        addedEntitlement.Should().NotBeNull();
        addedEntitlement!.Status.Should().Be(FeatureEntitlementStatus.PendingPayment);
        addedEntitlement.StripeCheckoutSessionId.Should().Be(SessionId);
        addedEntitlement.AmountMinor.Should().Be(capturedRequest.AmountMinor);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenExistingCustomer_ReusesItWithoutCreatingAnother()
    {
        _entitlementRepository.Setup(r => r.GetActiveAsync(
                TenantId, BillingFeature.StockReport, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FeatureEntitlement?)null);
        PartnerBillingCustomer existing = PartnerBillingCustomer.Create(TenantId, CustomerId).Value;
        _billingCustomerRepository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        Result<CreateStockReportCheckoutResult> result =
            await _handler.Handle(new CreateStockReportCheckoutCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _gateway.Verify(g => g.CreateCustomerAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _billingCustomerRepository.Verify(r => r.Add(It.IsAny<PartnerBillingCustomer>()), Times.Never);
    }
}
