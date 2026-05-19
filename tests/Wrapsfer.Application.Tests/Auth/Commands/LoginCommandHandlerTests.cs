using Wrapsfer.Application.Abstractions.IdentityProvisioning;
using Wrapsfer.Application.Auth.Commands.Login;
using Wrapsfer.Application.Auth.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Application.Tests.Auth.Commands;

public class LoginCommandHandlerTests
{
    private readonly Mock<IIdentityAuthService> _authService = new();
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests() => _handler = new LoginCommandHandler(_authService.Object);

    [Fact]
    public async Task Handle_WhenAuthServiceSucceeds_ReturnsLoginResponseWithClaims()
    {
        IdentityLoginResult identity = new(
            "jwt-token-value",
            ExpiresIn: 300,
            RefreshExpiresIn: 36000,
            RefreshToken: "refresh-token-value",
            TokenType: "Bearer",
            NotBeforePolicy: 0,
            SessionState: "session-id",
            Scope: "email profile",
            "user-uuid",
            "alphaadmin",
            RequiresPasswordChange: true);

        _authService
            .Setup(s => s.LoginAsync("partner-alpha", "alphaadmin", "pwd", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IdentityLoginResult>.Success(identity));

        Result<LoginResponse> result = await _handler.Handle(
            new LoginCommand("partner-alpha", "alphaadmin", "pwd"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Success.Should().BeTrue();
        result.Value.Token.Should().Be("jwt-token-value");
        result.Value.AccessToken.Should().Be("jwt-token-value");
        result.Value.ExpiresIn.Should().Be(300);
        result.Value.RefreshExpiresIn.Should().Be(36000);
        result.Value.RefreshToken.Should().Be("refresh-token-value");
        result.Value.TokenType.Should().Be("Bearer");
        result.Value.NotBeforePolicy.Should().Be(0);
        result.Value.SessionState.Should().Be("session-id");
        result.Value.Scope.Should().Be("email profile");
        result.Value.User.Id.Should().Be("user-uuid");
        result.Value.User.Username.Should().Be("alphaadmin");
        result.Value.User.RequiresPasswordChange.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenAuthServiceFails_PropagatesError()
    {
        _authService
            .Setup(s => s.LoginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IdentityLoginResult>.Failure(AuthErrors.InvalidCredentials));

        Result<LoginResponse> result = await _handler.Handle(
            new LoginCommand("partner-alpha", "alphaadmin", "wrong"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AuthErrors.InvalidCredentials.Code);
    }
}
