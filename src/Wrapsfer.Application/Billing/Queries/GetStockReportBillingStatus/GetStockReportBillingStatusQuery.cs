using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.Billing.Queries.GetStockReportBillingStatus;

public sealed record GetStockReportBillingStatusQuery : IQuery<StockReportBillingStatusResult>;
