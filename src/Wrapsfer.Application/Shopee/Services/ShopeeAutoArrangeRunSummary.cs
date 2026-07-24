namespace Wrapsfer.Application.Shopee.Services;

public sealed record ShopeeAutoArrangeRunSummary(
    int TenantsExamined,
    int RelinkAttempts,
    int OrdersArranged,
    int OrdersFailed,
    int OrdersSkipped);
