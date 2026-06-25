using FluentValidation;
using Wrapsfer.Application.Billing.StockReportDownloads;

namespace Wrapsfer.Application.Billing.Queries.GetPartnerStockReportDownloadCounts;

public sealed class GetPartnerStockReportDownloadCountsQueryValidator
    : AbstractValidator<GetPartnerStockReportDownloadCountsQuery>
{
    public GetPartnerStockReportDownloadCountsQueryValidator()
    {
        RuleFor(query => query.TenantId)
            .NotEmpty()
            .WithMessage("tenantId is required.");

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
