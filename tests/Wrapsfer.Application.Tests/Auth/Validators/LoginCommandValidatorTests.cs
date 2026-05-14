using FluentValidation.Results;
using Wrapsfer.Application.Auth.Commands.Login;

namespace Wrapsfer.Application.Tests.Auth.Validators;

public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_Succeeds()
    {
        ValidationResult result = _validator.Validate(new LoginCommand("partner-alpha", "alphaadmin", "pwd"));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "alphaadmin", "pwd", "Realm")]
    [InlineData("partner-alpha", "", "pwd", "Username")]
    [InlineData("partner-alpha", "alphaadmin", "", "Password")]
    public void Validate_RejectsEmptyFields(string realm, string username, string password, string field)
    {
        ValidationResult result = _validator.Validate(new LoginCommand(realm, username, password));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == field);
    }
}
