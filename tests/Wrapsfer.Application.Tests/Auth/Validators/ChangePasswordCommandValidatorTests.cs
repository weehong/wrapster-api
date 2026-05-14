using FluentValidation.Results;
using Wrapsfer.Application.Auth.Commands.ChangePassword;

namespace Wrapsfer.Application.Tests.Auth.Validators;

public class ChangePasswordCommandValidatorTests
{
    private readonly ChangePasswordCommandValidator _validator = new();

    private static ChangePasswordCommand ValidCommand() =>
        new("partner-alpha", "user-uuid", "alphaadmin", "OldPassword-12!", "NewPassword-34!");

    [Fact]
    public void Validate_WithValidCommand_Succeeds()
    {
        ValidationResult result = _validator.Validate(ValidCommand());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_RejectsShortNewPassword()
    {
        ChangePasswordCommand command = ValidCommand() with { NewPassword = "short" };

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NewPassword");
    }

    [Theory]
    [InlineData("", "user-uuid", "alphaadmin", "OldPassword-12!", "NewPassword-34!", "Realm")]
    [InlineData("partner-alpha", "", "alphaadmin", "OldPassword-12!", "NewPassword-34!", "UserId")]
    [InlineData("partner-alpha", "user-uuid", "", "OldPassword-12!", "NewPassword-34!", "Username")]
    [InlineData("partner-alpha", "user-uuid", "alphaadmin", "", "NewPassword-34!", "CurrentPassword")]
    public void Validate_RejectsEmptyFields(string realm, string userId, string username, string current, string next, string field)
    {
        ValidationResult result = _validator.Validate(
            new ChangePasswordCommand(realm, userId, username, current, next));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == field);
    }
}
