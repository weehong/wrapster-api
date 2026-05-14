using Wrapsfer.Domain.Common;

namespace Wrapsfer.Application.Abstractions.IdentityProvisioning;

public interface IIdentityAuthService
{
    Task<Result<IdentityLoginResult>> LoginAsync(
        string realm,
        string username,
        string password,
        CancellationToken cancellationToken = default);

    Task<Result> ChangePasswordAsync(
        string realm,
        string userId,
        string username,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);
}
