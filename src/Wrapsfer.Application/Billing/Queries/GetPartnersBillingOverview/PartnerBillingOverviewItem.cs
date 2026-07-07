using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Application.Billing.Queries.GetPartnersBillingOverview;

/// <summary>
/// A single partner's billing status for one billable feature. <see cref="FeatureKey"/> is the
/// stable string name of <see cref="Feature"/> so the owner UI can render a feature column and
/// gain new features without a contract change. When access is inactive, <see cref="AmountMinorDue"/>
/// carries the prorated price to purchase for the remainder of the month.
/// </summary>
public sealed record PartnerBillingOverviewItem(
    string TenantId,
    string DisplayName,
    BillingFeature Feature,
    string FeatureKey,
    bool IsActive,
    DateOnly? AccessStartDate,
    DateOnly? AccessEndDate,
    int? AmountMinorDue,
    string Currency);
