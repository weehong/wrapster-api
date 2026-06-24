using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Billing.Queries.GetStockReportBillingStatus;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Billing.Queries.GetPartnersBillingOverview;

internal sealed class GetPartnersBillingOverviewQueryHandler(
    IPartnerTenantRepository partnerTenantRepository,
    IFeatureEntitlementRepository entitlementRepository,
    StockReportBillingStatusFactory stockReportStatusFactory)
    : IQueryHandler<GetPartnersBillingOverviewQuery, IReadOnlyList<PartnerBillingOverviewItem>>
{
    public async Task<Result<IReadOnlyList<PartnerBillingOverviewItem>>> Handle(
        GetPartnersBillingOverviewQuery request,
        CancellationToken cancellationToken)
    {
        DateTime utcNow = DateTime.UtcNow;

        IReadOnlyList<PartnerTenant> partners =
            await partnerTenantRepository.ListAsync(isActive: null, cancellationToken);

        List<PartnerBillingOverviewItem> items = [];

        // One block of rows per billable feature. Adding a feature means adding a branch below;
        // the response contract and the owner UI need no change.
        foreach (BillingFeature feature in Enum.GetValues<BillingFeature>())
        {
            items.AddRange(feature switch
            {
                BillingFeature.StockReport =>
                    await BuildStockReportRowsAsync(partners, utcNow, cancellationToken),
                _ => [],
            });
        }

        return Result<IReadOnlyList<PartnerBillingOverviewItem>>.Success(items);
    }

    private async Task<IReadOnlyList<PartnerBillingOverviewItem>> BuildStockReportRowsAsync(
        IReadOnlyList<PartnerTenant> partners,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<FeatureEntitlement> activeEntitlements =
            await entitlementRepository.ListActiveAsync(BillingFeature.StockReport, utcNow, cancellationToken);

        Dictionary<string, FeatureEntitlement> activeByTenant = activeEntitlements
            .GroupBy(e => e.TenantId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(e => e.ValidToUtc).First());

        List<PartnerBillingOverviewItem> rows = new(partners.Count);
        foreach (PartnerTenant partner in partners)
        {
            activeByTenant.TryGetValue(partner.TenantId, out FeatureEntitlement? active);
            StockReportBillingStatusResult status = stockReportStatusFactory.Build(active, utcNow);

            rows.Add(new PartnerBillingOverviewItem(
                partner.TenantId,
                partner.DisplayName,
                BillingFeature.StockReport,
                nameof(BillingFeature.StockReport),
                status.IsActive,
                status.AccessStartDate,
                status.AccessEndDate,
                status.AmountMinorDue,
                status.Currency));
        }

        return rows;
    }
}
