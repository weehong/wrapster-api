using FluentValidation.Results;
using Wrapsfer.Application.Partners.Commands.CreatePartner;

namespace Wrapsfer.Application.Tests.Partners.Validators;

public class CreatePartnerCommandValidatorTests
{
    private readonly CreatePartnerCommandValidator _validator = new();

    private static CreatePartnerCommand ValidCommand() =>
        new("partner-acme", "Acme", "admin@acme.example", "acmeadmin", "TempPass1234!", true);

    [Fact]
    public void Validate_WithValidCommand_Succeeds()
    {
        ValidationResult result = _validator.Validate(ValidCommand());

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("AB", "TenantId")]
    [InlineData("Partner-ACME", "TenantId")]
    [InlineData("partner_acme", "TenantId")]
    [InlineData("-partner", "TenantId")]
    [InlineData("partner-", "TenantId")]
    public void Validate_RejectsInvalidTenantId(string tenantId, string field)
    {
        CreatePartnerCommand command = ValidCommand() with { TenantId = tenantId };

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == field);
    }

    [Fact]
    public void Validate_RejectsInvalidEmail()
    {
        CreatePartnerCommand command = ValidCommand() with { AdminEmail = "not-an-email" };

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "AdminEmail");
    }

    [Fact]
    public void Validate_RejectsShortPassword()
    {
        CreatePartnerCommand command = ValidCommand() with { TemporaryPassword = "short" };

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "TemporaryPassword");
    }

    [Theory]
    [InlineData("acme admin")]
    [InlineData(" acmeadmin")]
    [InlineData("acme\tadmin")]
    [InlineData("acme&admin")]
    [InlineData("acme/admin")]
    [InlineData("ab")]
    public void Validate_RejectsInvalidAdminUsername(string adminUsername)
    {
        CreatePartnerCommand command = ValidCommand() with { AdminUsername = adminUsername };

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "AdminUsername");
    }

    [Theory]
    [InlineData("acmeadmin")]
    [InlineData("acme.admin_1")]
    [InlineData("acme-admin")]
    public void Validate_AcceptsKeycloakSafeAdminUsername(string adminUsername)
    {
        CreatePartnerCommand command = ValidCommand() with { AdminUsername = adminUsername };

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
