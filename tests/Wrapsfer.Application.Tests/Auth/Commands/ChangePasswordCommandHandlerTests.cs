using Wrapsfer.Application.Abstractions.IdentityProvisioning;
using Wrapsfer.Application.Auth.Commands.ChangePassword;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Application.Tests.Auth.Commands;

public class ChangePasswordCommandHandlerTests
{
    private readonly Mock<IIdentityAuthService> _authService = new();
    private readonly ChangePasswordCommandHandler _handler;

    public ChangePasswordCommandHandlerTests() => _handler = new ChangePasswordCommandHandler(_authService.Object);

    private static ChangePasswordCommand Command(
        string current = "OldPassword-12!",
        string next = "NewPassword-34!") =>
        new("partner-alpha", "user-uuid", "alphaadmin", current, next);

    [Fact]
    public async Task Handle_WhenNewPasswordEqualsCurrent_ReturnsSamePasswordError()
    {
        Result result = await _handler.Handle(
            Command(current: "SamePassword-12!", next: "SamePassword-12!"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AuthErrors.NewPasswordSameAsCurrent.Code);
        _authService.Verify(
            s => s.ChangePasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAuthServiceRejectsCurrentPassword_ReturnsInvalidCredentials()
    {
        _authService
            .Setup(s => s.ChangePasswordAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(AuthErrors.InvalidCredentials));

        Result result = await _handler.Handle(Command(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AuthErrors.InvalidCredentials.Code);
    }

    [Fact]
    public async Task Handle_WhenAuthServiceSucceeds_ReturnsSuccess()
    {
        _authService
            .Setup(s => s.ChangePasswordAsync(
                "partner-alpha", "user-uuid", "alphaadmin", "OldPassword-12!", "NewPassword-34!", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        Result result = await _handler.Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }
}
