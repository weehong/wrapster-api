using FluentValidation;
using Wrapsfer.Application.Billing.StockReportDownloads;

namespace Wrapsfer.Application.Billing.Queries.GetStockReportDownloadCounts;

public sealed class GetStockReportDownloadCountsQueryValidator
    : AbstractValidator<GetStockReportDownloadCountsQuery>
{
    public GetStockReportDownloadCountsQueryValidator()
    {
        RuleFor(query => query.FromMonth)
            .Must(StockReportDownloadMonthRange.IsValidMonth)
            .WithMessage("fromMonth must be a valid month in YYYY-MM format.");

        RuleFor(query => query.ToMonth)
            .Must(StockReportDownloadMonthRange.IsValidMonth)
            .WithMessage("toMonth must be a valid month in YYYY-MM format.");

        RuleFor(query => query)
            .Must(query => StockReportDownloadMonthRange.IsOrderedRange(query.FromMonth, query.ToMonth))
            .WithMessage("fromMonth must not be after toMonth.")
            .When(query => StockReportDownloadMonthRange.IsValidMonth(query.FromMonth)
                           && StockReportDownloadMonthRange.IsValidMonth(query.ToMonth));
    }
}
