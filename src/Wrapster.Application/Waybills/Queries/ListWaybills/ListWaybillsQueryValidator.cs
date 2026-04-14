using FluentValidation;

namespace Wrapster.Application.Waybills.Queries.ListWaybills;

public sealed class ListWaybillsQueryValidator : AbstractValidator<ListWaybillsQuery>
{
    public ListWaybillsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x).Must(q => !q.From.HasValue || !q.To.HasValue || q.From.Value <= q.To.Value)
            .WithMessage("From date must be less than or equal to To date.");
    }
}
