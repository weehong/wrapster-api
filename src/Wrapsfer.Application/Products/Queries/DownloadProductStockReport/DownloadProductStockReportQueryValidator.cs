using FluentValidation;

namespace Wrapsfer.Application.Products.Queries.DownloadProductStockReport;

public sealed class DownloadProductStockReportQueryValidator : AbstractValidator<DownloadProductStockReportQuery>
{
    public DownloadProductStockReportQueryValidator()
    {
        RuleFor(x => x.Format)
            .IsInEnum();

        RuleFor(x => x.AsOfDate)
            .Must(date => date <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("As-of date cannot be in the future.");
    }
}
