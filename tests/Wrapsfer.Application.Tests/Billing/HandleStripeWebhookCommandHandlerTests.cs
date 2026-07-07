using Microsoft.Extensions.Logging.Abstractions;
using Wrapsfer.Application.Billing;
using Wrapsfer.Application.Billing.Abstractions;
using Wrapsfer.Application.Billing.Commands.HandleStripeWebhook;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Billing;

public class HandleStripeWebhookCommandHandlerTests
{
    private const string TenantId = "partner-acme";
    private const string SessionId = "cs_test_123";
    private const string EventId = "evt_test_123";

    private readonly Mock<IStripeBillingGateway> _gateway = new();
    private readonly Mock<IStripeWebhookEventRepository> _webhookEventRepository = new();
    private readonly Mock<IFeatureEntitlementRepository> _entitlementRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly HandleStripeWebhookCommandHandler _handler;

    public HandleStripeWebhookCommandHandlerTests()
    {
        _handler = new HandleStripeWebhookCommandHandler(
            _gateway.Object,
            _webhookEventRepository.Object,
            _entitlementRepository.Object,
            _unitOfWork.Object,
            NullLogger<HandleStripeWebhookCommandHandler>.Instance);
    }

    private static FeatureEntitlement PendingEntitlement()
    {
        DateTime now = DateTime.UtcNow;
        return FeatureEntitlement.CreatePending(
            TenantId, BillingFeature.StockReport, now.AddDays(-1), now.AddDays(20), 267, "myr", SessionId).Value;
    }

    private void SetUpNotification(string eventType, string? sessionId)
    {
        StripeWebhookNotification notification = new(
            EventId, eventType, sessionId, "pi_test", "in_test", "cus_test",
            new Dictionary<string, string>());
        _gateway.Setup(g => g.ParseWebhookEvent(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Result<StripeWebhookNotification>.Success(notification));
    }

    [Fact]
    public async Task Handle_WhenSignatureInvalid_ReturnsFailureAndPersistsNothing()
    {
        _gateway.Setup(g => g.ParseWebhookEvent(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Result<StripeWebhookNotification>.Failure(BillingErrors.WebhookSignatureInvalid));

        Result result = await _handler.Handle(
            new HandleStripeWebhookCommand("{}", "bad-signature"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BillingErrors.WebhookSignatureInvalid);
        _webhookEventRepository.Verify(r => r.Add(It.IsAny<StripeWebhookEvent>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenEventAlreadyProcessed_IsNoOpAndReturnsSuccess()
    {
        SetUpNotification(StripeEventTypes.CheckoutSessionCompleted, SessionId);
        _webhookEventRepository.Setup(r => r.ExistsAsync(EventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Result result = await _handler.Handle(
            new HandleStripeWebhookCommand("{}", "sig"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _entitlementRepository.Verify(
            r => r.GetByCheckoutSessionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _webhookEventRepository.Verify(r => r.Add(It.IsAny<StripeWebhookEvent>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCheckoutCompleted_ActivatesEntitlementAndRecordsEvent()
    {
        SetUpNotification(StripeEventTypes.CheckoutSessionCompleted, SessionId);
        _webhookEventRepository.Setup(r => r.ExistsAsync(EventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        FeatureEntitlement entitlement = PendingEntitlement();
        _entitlementRepository.Setup(r => r.GetByCheckoutSessionIdAsync(SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entitlement);

        Result result = await _handler.Handle(
            new HandleStripeWebhookCommand("{}", "sig"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        entitlement.Status.Should().Be(FeatureEntitlementStatus.Active);
        entitlement.StripePaymentIntentId.Should().Be("pi_test");
        entitlement.StripeInvoiceId.Should().Be("in_test");
        _webhookEventRepository.Verify(
            r => r.Add(It.Is<StripeWebhookEvent>(e => e.StripeEventId == EventId)), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(StripeEventTypes.ChargeRefunded)]
    [InlineData(StripeEventTypes.RefundCreated)]
    public async Task Handle_WhenRefundEvent_CancelsEntitlementAndRecordsEvent(string eventType)
    {
        SetUpNotification(eventType, sessionId: null);
        _webhookEventRepository.Setup(r => r.ExistsAsync(EventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        FeatureEntitlement entitlement = PendingEntitlement();
        entitlement.Activate("pi_test", "in_test");
        _entitlementRepository.Setup(r => r.GetByPaymentIntentIdAsync("pi_test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(entitlement);

        Result result = await _handler.Handle(
            new HandleStripeWebhookCommand("{}", "sig"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        entitlement.Status.Should().Be(FeatureEntitlementStatus.Canceled);
        _webhookEventRepository.Verify(
            r => r.Add(It.Is<StripeWebhookEvent>(e => e.StripeEventId == EventId)), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUnknownSession_StillRecordsEventWithoutThrowing()
    {
        SetUpNotification(StripeEventTypes.CheckoutSessionCompleted, "cs_unknown");
        _webhookEventRepository.Setup(r => r.ExistsAsync(EventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _entitlementRepository.Setup(r => r.GetByCheckoutSessionIdAsync("cs_unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync((FeatureEntitlement?)null);

        Result result = await _handler.Handle(
            new HandleStripeWebhookCommand("{}", "sig"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _webhookEventRepository.Verify(r => r.Add(It.IsAny<StripeWebhookEvent>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUnhandledEventType_RecordsEventOnly()
    {
        SetUpNotification("payment_intent.succeeded", null);
        _webhookEventRepository.Setup(r => r.ExistsAsync(EventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Result result = await _handler.Handle(
            new HandleStripeWebhookCommand("{}", "sig"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _entitlementRepository.Verify(
            r => r.GetByCheckoutSessionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _webhookEventRepository.Verify(r => r.Add(It.IsAny<StripeWebhookEvent>()), Times.Once);
    }
}
