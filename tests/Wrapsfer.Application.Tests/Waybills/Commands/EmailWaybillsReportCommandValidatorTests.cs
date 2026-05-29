using FluentValidation.Results;
using Wrapsfer.Application.Waybills.Commands.EmailWaybillsReport;
using Wrapsfer.Application.Waybills.Commands.RequestWaybillsExport;

namespace Wrapsfer.Application.Tests.Waybills.Commands;

public class EmailWaybillsReportCommandValidatorTests
{
    private readonly EmailWaybillsReportCommandValidator _validator = new();

    private static EmailWaybillsReportCommand Build(
        IReadOnlyList<string>? recipients = null,
        WaybillExportFormat format = WaybillExportFormat.Csv,
        DateOnly? from = null,
        DateOnly? to = null) => new(
        format,
        recipients ?? new[] { "ops@example.com" },
        from, to, null, null,
        IncludeAllPartnerTenants: false);

    [Fact]
    public void Validate_WithRecipientAndCsv_Passes()
    {
        ValidationResult result = _validator.Validate(Build());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyRecipientList_Fails()
    {
        ValidationResult result = _validator.Validate(Build(Array.Empty<string>()));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(EmailWaybillsReportCommand.RecipientEmails));
    }

    [Fact]
    public void Validate_WithMalformedEmail_Fails()
    {
        ValidationResult result = _validator.Validate(Build(new[] { "not-an-email" }));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.StartsWith("RecipientEmails"));
    }

    [Fact]
    public void Validate_WithUndefinedFormat_Fails()
    {
        ValidationResult result = _validator.Validate(Build(format: (WaybillExportFormat)999));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(EmailWaybillsReportCommand.Format));
    }

    [Fact]
    public void Validate_WithInvertedDateRange_Fails()
    {
        ValidationResult result = _validator.Validate(
            Build(from: new DateOnly(2026, 5, 10), to: new DateOnly(2026, 5, 1)));

        result.IsValid.Should().BeFalse();
    }
}
