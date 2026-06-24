using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Billing.Responses;

namespace Wrapsfer.Application.Billing.Queries.ListBillingHistory;

public sealed record ListBillingHistoryQuery : IQuery<IReadOnlyList<BillingHistoryItemResponse>>;
