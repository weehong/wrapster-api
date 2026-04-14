using FluentValidation;

namespace Wrapsfer.Application.StockAlerts.Queries.ListStockAlerts;

public sealed class ListStockAlertsQueryValidator : AbstractValidator<ListStockAlertsQuery>
{
    public ListStockAlertsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}
