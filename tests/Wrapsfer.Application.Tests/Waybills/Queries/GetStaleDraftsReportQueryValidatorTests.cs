using FluentValidation.Results;
using Wrapsfer.Application.Waybills.Queries.GetStaleDraftsReport;

namespace Wrapsfer.Application.Tests.Waybills.Queries;

public class GetStaleDraftsReportQueryValidatorTests
{
    private readonly GetStaleDraftsReportQueryValidator _validator = new();

    [Fact]
    public void Validate_WithNoDates_Passes()
    {
        ValidationResult result = _validator.Validate(new GetStaleDraftsReportQuery(null, null));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithFromBeforeTo_Passes()
    {
        ValidationResult result = _validator.Validate(
            new GetStaleDraftsReportQuery(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30)));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithFromAfterTo_Fails()
    {
        ValidationResult result = _validator.Validate(
            new GetStaleDraftsReportQuery(new DateOnly(2026, 6, 30), new DateOnly(2026, 6, 1)));

        result.IsValid.Should().BeFalse();
    }
}
