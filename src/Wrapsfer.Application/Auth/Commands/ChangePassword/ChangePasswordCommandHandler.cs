using Wrapsfer.Application.Abstractions.IdentityProvisioning;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Application.Auth.Commands.ChangePassword;

internal sealed class ChangePasswordCommandHandler(IIdentityAuthService authService)
    : ICommandHandler<ChangePasswordCommand>
{
    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        if (string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal))
        {
            return Result.Failure(AuthErrors.NewPasswordSameAsCurrent);
        }

        return await authService.ChangePasswordAsync(
            request.Realm,
            request.UserId,
            request.Username,
            request.CurrentPassword,
            request.NewPassword,
            cancellationToken);
    }
}
