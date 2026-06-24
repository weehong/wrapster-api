using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.Billing.Queries.GetPartnersBillingOverview;

/// <summary>
/// Owner-facing query returning every partner's billing status for each billable feature,
/// one row per (partner x feature). Today only the stock report is billable.
/// </summary>
public sealed record GetPartnersBillingOverviewQuery
    : IQuery<IReadOnlyList<PartnerBillingOverviewItem>>;
