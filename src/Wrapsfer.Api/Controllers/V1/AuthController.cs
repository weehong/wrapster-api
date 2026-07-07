using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wrapsfer.Api.Contracts;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Auth.Commands.ChangePassword;
using Wrapsfer.Application.Auth.Commands.Login;
using Wrapsfer.Application.Auth.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Infrastructure.Authentication;

namespace Wrapsfer.Api.Controllers.V1;

public sealed class AuthController(ISender sender) : ApiControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        [FromServices] ITenantRealmResolver realmResolver,
        CancellationToken cancellationToken)
    {
        string realm = realmResolver.ResolveRealm(HttpContext.Request);

        LoginCommand command = new(realm, request.Username, request.Password);

        Result<LoginResponse> result = await sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize]
    [AllowRestrictedToken]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        [FromServices] ITenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        ChangePasswordCommand command = new(
            tenantContext.TenantId,
            tenantContext.UserId,
            tenantContext.Username,
            request.CurrentPassword,
            request.NewPassword);

        Result result = await sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }
}
