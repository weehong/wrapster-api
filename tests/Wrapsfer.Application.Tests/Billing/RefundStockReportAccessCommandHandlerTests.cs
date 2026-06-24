using Wrapsfer.Application.Billing.Abstractions;
using Wrapsfer.Application.Billing.Commands.RefundStockReportAccess;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Billing;

public class RefundStockReportAccessCommandHandlerTests
{
    private const string TenantId = "partner-acme";
    private const string PaymentIntentId = "pi_test";

    private readonly Mock<IFeatureEntitlementRepository> _entitlementRepository = new();
    private readonly Mock<IStripeBillingGateway> _gateway = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly RefundStockReportAccessCommandHandler _handler;

    public RefundStockReportAccessCommandHandlerTests()
    {
        _handler = new RefundStockReportAccessCommandHandler(
            _entitlementRepository.Object,
            _gateway.Object,
            _unitOfWork.Object);
    }

    [Fact]
    public async Task Handle_WhenNoActiveEntitlement_ReturnsNotFound()
    {
        _entitlementRepository.Setup(r => r.GetActiveAsync(
                TenantId, BillingFeature.StockReport, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FeatureEntitlement?)null);

        Result result = await _handler.Handle(
            new RefundStockReportAccessCommand(TenantId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BillingErrors.StockReportActiveEntitlementNotFound);
        _gateway.Verify(g => g.RefundPaymentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenActiveEntitlementHasNoPaymentIntent_ReturnsConflict()
    {
        FeatureEntitlement entitlement = ActiveEntitlement(stripePaymentIntentId: null);
        _entitlementRepository.Setup(r => r.GetActiveAsync(
                TenantId, BillingFeature.StockReport, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entitlement);

        Result result = await _handler.Handle(
            new RefundStockReportAccessCommand(TenantId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BillingErrors.StockReportPaymentNotRefundable);
        entitlement.Status.Should().Be(FeatureEntitlementStatus.Active);
        _gateway.Verify(g => g.RefundPaymentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenActiveEntitlementHasPaymentIntent_RefundsAndCancelsAccess()
    {
        FeatureEntitlement entitlement = ActiveEntitlement(PaymentIntentId);
        _entitlementRepository.Setup(r => r.GetActiveAsync(
                TenantId, BillingFeature.StockReport, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entitlement);
        _gateway.Setup(g => g.RefundPaymentAsync(PaymentIntentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("re_test");

        Result result = await _handler.Handle(
            new RefundStockReportAccessCommand(TenantId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        entitlement.Status.Should().Be(FeatureEntitlementStatus.Canceled);
        _gateway.Verify(g => g.RefundPaymentAsync(PaymentIntentId, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static FeatureEntitlement ActiveEntitlement(string? stripePaymentIntentId)
    {
        FeatureEntitlement entitlement = FeatureEntitlement.CreatePending(
            TenantId,
            BillingFeature.StockReport,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(10),
            233,
            "myr",
            "cs_test").Value;
        entitlement.Activate(stripePaymentIntentId, "in_test");
        return entitlement;
    }
}
