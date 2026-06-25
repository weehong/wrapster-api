using FluentValidation.Results;
using Wrapsfer.Application.Billing.Queries.GetStockReportDownloadCounts;

namespace Wrapsfer.Application.Tests.Billing.Queries;

public class GetStockReportDownloadCountsQueryValidatorTests
{
    private readonly GetStockReportDownloadCountsQueryValidator _validator = new();

    [Theory]
    [InlineData("2026-05", "2026-06")]
    [InlineData("2026-01", "2026-01")]
    public void Validate_WithValidOrderedMonths_Passes(string fromMonth, string toMonth)
    {
        ValidationResult result = _validator.Validate(new GetStockReportDownloadCountsQuery(fromMonth, toMonth));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "2026-06")]
    [InlineData("2026-13", "2026-06")]
    [InlineData("2026/06", "2026-06")]
    [InlineData("2026-06", "not-a-month")]
    public void Validate_WithMalformedMonth_Fails(string fromMonth, string toMonth)
    {
        ValidationResult result = _validator.Validate(new GetStockReportDownloadCountsQuery(fromMonth, toMonth));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WhenFromMonthAfterToMonth_Fails()
    {
        ValidationResult result = _validator.Validate(new GetStockReportDownloadCountsQuery("2026-07", "2026-06"));

        result.IsValid.Should().BeFalse();
    }
}
