using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Billing.Abstractions;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Billing.Commands.RefundStockReportAccess;

internal sealed class RefundStockReportAccessCommandHandler(
    IFeatureEntitlementRepository entitlementRepository,
    IStripeBillingGateway billingGateway,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RefundStockReportAccessCommand>
{
    public async Task<Result> Handle(
        RefundStockReportAccessCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId))
        {
            return Result.Failure(BillingErrors.InvalidTenantId);
        }

        FeatureEntitlement? entitlement = await entitlementRepository.GetActiveAsync(
            request.TenantId,
            BillingFeature.StockReport,
            DateTime.UtcNow,
            cancellationToken);

        if (entitlement is null)
        {
            return Result.Failure(BillingErrors.StockReportActiveEntitlementNotFound);
        }

        if (string.IsNullOrWhiteSpace(entitlement.StripePaymentIntentId))
        {
            return Result.Failure(BillingErrors.StockReportPaymentNotRefundable);
        }

        await billingGateway.RefundPaymentAsync(entitlement.StripePaymentIntentId, cancellationToken);
        entitlement.Cancel();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
