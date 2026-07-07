using Wrapsfer.Application.Abstractions.IdentityProvisioning;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Auth.Responses;
using Wrapsfer.Domain.Common;

namespace Wrapsfer.Application.Auth.Commands.Login;

internal sealed class LoginCommandHandler(IIdentityAuthService authService)
    : ICommandHandler<LoginCommand, LoginResponse>
{
    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        Result<IdentityLoginResult> loginResult =
            await authService.LoginAsync(request.Realm, request.Username, request.Password, cancellationToken);

        if (loginResult.IsFailure)
        {
            return Result<LoginResponse>.Failure(loginResult.Error);
        }

        IdentityLoginResult identity = loginResult.Value;

        LoginUserPayload user = new(
            identity.UserId,
            identity.Username,
            identity.RequiresPasswordChange);

        LoginResponse response = new(
            true,
            identity.AccessToken,
            identity.AccessToken,
            identity.ExpiresIn,
            identity.RefreshExpiresIn,
            identity.RefreshToken,
            identity.TokenType,
            identity.NotBeforePolicy,
            identity.SessionState,
            identity.Scope,
            user);

        return Result<LoginResponse>.Success(response);
    }
}
