using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Billing.Queries.GetStockReportBillingStatus;

namespace Wrapsfer.Application.Billing.Queries.GetPartnerStockReportBillingStatus;

public sealed record GetPartnerStockReportBillingStatusQuery(string TenantId)
    : IQuery<StockReportBillingStatusResult>;
