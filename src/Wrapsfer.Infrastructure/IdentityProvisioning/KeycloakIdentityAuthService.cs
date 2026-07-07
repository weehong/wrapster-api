using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wrapsfer.Application.Abstractions.IdentityProvisioning;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Infrastructure.Authentication;
using Wrapsfer.Infrastructure.IdentityProvisioning.Dtos;

namespace Wrapsfer.Infrastructure.IdentityProvisioning;

internal sealed class KeycloakIdentityAuthService : IIdentityAuthService
{
    private readonly KeycloakAdminHttpClient _adminHttp;
    private readonly KeycloakOptions _options;
    private readonly ILogger<KeycloakIdentityAuthService> _logger;

    public KeycloakIdentityAuthService(
        KeycloakAdminHttpClient adminHttp,
        IOptions<KeycloakOptions> options,
        ILogger<KeycloakIdentityAuthService> logger)
    {
        _adminHttp = adminHttp;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<IdentityLoginResult>> LoginAsync(
        string realm,
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        EnsurePartnerApiClientSecretConfigured();

        using HttpClient client = _adminHttp.CreateClient();

        KeycloakTokenResponse? tokenResponse =
            await RequestPasswordGrantAsync(client, realm, username, password, cancellationToken);

        if (tokenResponse is null)
        {
            return Result<IdentityLoginResult>.Failure(AuthErrors.InvalidCredentials);
        }

        JwtSecurityTokenHandler handler = new() { MapInboundClaims = false };
        JwtSecurityToken jwt = handler.ReadJwtToken(tokenResponse.AccessToken);

        string userId = jwt.Subject ?? string.Empty;
        string resolvedUsername = jwt.Claims
                                      .FirstOrDefault(c => string.Equals(c.Type, "preferred_username", StringComparison.Ordinal))
                                      ?.Value
                                  ?? username;
        bool requiresPasswordChange = ReadBooleanClaim(jwt, KeycloakTenantProvisioningService.RequiresPasswordChangeAttribute);

        IdentityLoginResult result = new(
            tokenResponse.AccessToken,
            tokenResponse.ExpiresIn,
            tokenResponse.RefreshExpiresIn,
            tokenResponse.RefreshToken,
            tokenResponse.TokenType,
            tokenResponse.NotBeforePolicy,
            tokenResponse.SessionState,
            tokenResponse.Scope,
            userId,
            resolvedUsername,
            requiresPasswordChange);

        return Result<IdentityLoginResult>.Success(result);
    }

    public async Task<Result> ChangePasswordAsync(
        string realm,
        string userId,
        string username,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        EnsurePartnerApiClientSecretConfigured();

        using (HttpClient verifyClient = _adminHttp.CreateClient())
        {
            KeycloakTokenResponse? verifyResponse =
                await RequestPasswordGrantAsync(verifyClient, realm, username, currentPassword, cancellationToken);
            if (verifyResponse is null)
            {
                return Result.Failure(AuthErrors.InvalidCredentials);
            }
        }

        try
        {
            using HttpClient adminClient = await _adminHttp.CreateAuthorizedClientAsync(cancellationToken);

            KeycloakCredentialRepresentation credential = new()
            {
                Type = "password",
                Value = newPassword,
                Temporary = false
            };

            HttpResponseMessage resetResponse = await adminClient.PutAsJsonAsync(
                $"admin/realms/{Uri.EscapeDataString(realm)}/users/{Uri.EscapeDataString(userId)}/reset-password",
                credential,
                cancellationToken);

            await _adminHttp.EnsureSuccessAsync(resetResponse, $"reset password for user {userId} in realm {realm}", cancellationToken);

            await ClearRequiresPasswordChangeAttributeAsync(adminClient, realm, userId, cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to change password for user {UserId} in realm {Realm}",
                userId, realm);
            return Result.Failure(AuthErrors.PasswordChangeFailed);
        }
    }

    private async Task ClearRequiresPasswordChangeAttributeAsync(
        HttpClient adminClient,
        string realm,
        string userId,
        CancellationToken cancellationToken)
    {
        // Keycloak replaces the user's attributes wholesale on PUT, so we GET first,
        // remove only the requires_password_change key, then PUT the modified payload back.
        HttpResponseMessage getResponse = await adminClient.GetAsync(
            $"admin/realms/{Uri.EscapeDataString(realm)}/users/{Uri.EscapeDataString(userId)}",
            cancellationToken);

        await _adminHttp.EnsureSuccessAsync(getResponse, $"read user {userId} in realm {realm}", cancellationToken);

        KeycloakUserRepresentation? user =
            await getResponse.Content.ReadFromJsonAsync<KeycloakUserRepresentation>(cancellationToken);

        if (user is null)
        {
            throw new InvalidOperationException(
                $"Identity provider returned an empty user payload for {userId} in realm {realm}.");
        }

        user.Credentials = null;

        user.Attributes ??= new Dictionary<string, List<string>>();
        user.Attributes.Remove(KeycloakTenantProvisioningService.RequiresPasswordChangeAttribute);

        HttpResponseMessage putResponse = await adminClient.PutAsJsonAsync(
            $"admin/realms/{Uri.EscapeDataString(realm)}/users/{Uri.EscapeDataString(userId)}",
            user,
            cancellationToken);

        await _adminHttp.EnsureSuccessAsync(putResponse, $"clear requires_password_change attribute for user {userId} in realm {realm}", cancellationToken);
    }

    private async Task<KeycloakTokenResponse?> RequestPasswordGrantAsync(
        HttpClient client,
        string realm,
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string> form = new()
        {
            ["grant_type"] = "password",
            ["client_id"] = _options.Audience,
            ["client_secret"] = _options.PartnerApiClientSecret!,
            ["username"] = username,
            ["password"] = password
        };

        using FormUrlEncodedContent content = new(form);

        using HttpRequestMessage requestMessage = new(
            HttpMethod.Post,
            $"realms/{Uri.EscapeDataString(realm)}/protocol/openid-connect/token")
        {
            Content = content
        };

        requestMessage.Headers.Authorization = null;

        HttpResponseMessage response = await client.SendAsync(requestMessage, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<KeycloakTokenResponse>(cancellationToken);
        }

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation(
                "Keycloak password grant rejected for realm {Realm}: {Status} {Body}",
                realm, response.StatusCode, body);
            return null;
        }

        string serverBody = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogError(
            "Keycloak password grant failed unexpectedly for realm {Realm}: {Status} {Body}",
            realm, response.StatusCode, serverBody);

        throw new HttpRequestException(
            $"Keycloak password grant returned unexpected status {(int)response.StatusCode}.");
    }

    private static bool ReadBooleanClaim(JwtSecurityToken jwt, string claimType)
    {
        Claim? claim = jwt.Claims.FirstOrDefault(c => string.Equals(c.Type, claimType, StringComparison.Ordinal));
        if (claim is null)
        {
            return false;
        }

        return bool.TryParse(claim.Value, out bool parsed) && parsed;
    }

    private void EnsurePartnerApiClientSecretConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.PartnerApiClientSecret))
        {
            throw new InvalidOperationException(
                "Keycloak:PartnerApiClientSecret is not configured. The auth endpoints require it.");
        }
    }
}
