using FluentValidation.Results;
using Wrapsfer.Application.Partners.Commands.RetryPartnerProvisioning;

namespace Wrapsfer.Application.Tests.Partners.Validators;

public class RetryPartnerProvisioningCommandValidatorTests
{
    private readonly RetryPartnerProvisioningCommandValidator _validator = new();

    private static RetryPartnerProvisioningCommand ValidCommand() =>
        new("partner-acme", "Acme", "admin@acme.example", "acmeadmin", "TempPass1234!", true, "ops@acme.example");

    [Fact]
    public void Validate_WithValidCommand_Succeeds()
    {
        ValidationResult result = _validator.Validate(ValidCommand());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_RejectsEmptyDisplayName()
    {
        RetryPartnerProvisioningCommand command = ValidCommand() with { DisplayName = " " };

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DisplayName");
    }

    [Fact]
    public void Validate_RejectsInvalidContactEmail()
    {
        RetryPartnerProvisioningCommand command = ValidCommand() with { ContactEmail = "not-an-email" };

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ContactEmail");
    }
}
