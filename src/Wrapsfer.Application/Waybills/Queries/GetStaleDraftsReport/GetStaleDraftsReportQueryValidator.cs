using FluentValidation;

namespace Wrapsfer.Application.Waybills.Queries.GetStaleDraftsReport;

public sealed class GetStaleDraftsReportQueryValidator : AbstractValidator<GetStaleDraftsReportQuery>
{
    public GetStaleDraftsReportQueryValidator()
    {
        RuleFor(x => x).Must(q => !q.From.HasValue || !q.To.HasValue || q.From.Value <= q.To.Value)
            .WithMessage("From date must be less than or equal to To date.");
    }
}
