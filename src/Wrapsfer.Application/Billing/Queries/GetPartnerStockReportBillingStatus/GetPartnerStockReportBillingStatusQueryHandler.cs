using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Billing.Queries.GetStockReportBillingStatus;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Billing.Queries.GetPartnerStockReportBillingStatus;

internal sealed class GetPartnerStockReportBillingStatusQueryHandler(
    IFeatureEntitlementRepository entitlementRepository,
    StockReportBillingStatusFactory statusFactory)
    : IQueryHandler<GetPartnerStockReportBillingStatusQuery, StockReportBillingStatusResult>
{
    public async Task<Result<StockReportBillingStatusResult>> Handle(
        GetPartnerStockReportBillingStatusQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId))
        {
            return Result<StockReportBillingStatusResult>.Failure(BillingErrors.InvalidTenantId);
        }

        DateTime utcNow = DateTime.UtcNow;

        FeatureEntitlement? activeEntitlement = await entitlementRepository.GetActiveAsync(
            request.TenantId, BillingFeature.StockReport, utcNow, cancellationToken);

        return Result<StockReportBillingStatusResult>.Success(
            statusFactory.Build(activeEntitlement, utcNow));
    }
}
