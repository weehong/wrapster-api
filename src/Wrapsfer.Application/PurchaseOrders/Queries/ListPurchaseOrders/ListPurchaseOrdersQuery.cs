using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Common;
using Wrapsfer.Application.PurchaseOrders.Responses;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Application.PurchaseOrders.Queries.ListPurchaseOrders;

public sealed record ListPurchaseOrdersQuery(
    PurchaseOrderStatus? Status,
    string? Search,
    int Page = 1,
    int PageSize = 20,
    bool IncludeAllPartnerTenants = false) : IQuery<PagedResult<PurchaseOrderResponse>>;
