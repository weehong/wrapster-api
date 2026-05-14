using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wrapsfer.Application.Abstractions.IdentityProvisioning;
using Wrapsfer.Infrastructure.Authentication;
using Wrapsfer.Infrastructure.IdentityProvisioning.Dtos;

namespace Wrapsfer.Infrastructure.IdentityProvisioning;

internal sealed class KeycloakTenantProvisioningService : IIdentityTenantProvisioningService
{
    internal const string HttpClientName = "KeycloakAdmin";
    private const string DefaultAdminRealm = "master";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<KeycloakTenantProvisioningService> _logger;
    private readonly KeycloakOptions _options;

    public KeycloakTenantProvisioningService(
        IHttpClientFactory httpClientFactory,
        IOptions<KeycloakOptions> options,
        ILogger<KeycloakTenantProvisioningService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PartnerRealmProvisioningResult> CreatePartnerRealmAsync(
        PartnerRealmProvisioningRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureOnboardingEnabled();

        try
        {
            using HttpClient client = _httpClientFactory.CreateClient(HttpClientName);
            string token = await GetAdminTokenAsync(client, cancellationToken);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            await CreateRealmAsync(client, request.TenantId, request.DisplayName, cancellationToken);
            await CreateRealmRoleAsync(client, request.TenantId, "admin", "Administrator role", cancellationToken);
            await CreateRealmRoleAsync(client, request.TenantId, "user", "Standard user role", cancellationToken);
            await CreateApiClientAsync(client, request.TenantId, cancellationToken);
            string userId = await CreateAdminUserAsync(client, request, cancellationToken);
            await AssignAdminRoleAsync(client, request.TenantId, userId, cancellationToken);

            return new PartnerRealmProvisioningResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to provision Keycloak realm for tenant {TenantId}",
                request.TenantId);
            return new PartnerRealmProvisioningResult(false, "Provisioning request to identity provider failed");
        }
    }

    public async Task<PartnerRealmProvisioningResult> SetPartnerRealmActiveAsync(
        string tenantId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        EnsureOnboardingEnabled();

        try
        {
            using HttpClient client = _httpClientFactory.CreateClient(HttpClientName);
            string token = await GetAdminTokenAsync(client, cancellationToken);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            KeycloakRealmRepresentation payload = new()
            {
                Realm = tenantId,
                Enabled = isActive
            };

            HttpResponseMessage response = await client.PutAsJsonAsync(
                $"admin/realms/{Uri.EscapeDataString(tenantId)}",
                payload,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Keycloak rejected realm enable={IsActive} for tenant {TenantId}: {Status} {Body}",
                    isActive, tenantId, response.StatusCode, body);
                return new PartnerRealmProvisioningResult(false, "Identity provider rejected realm update");
            }

            return new PartnerRealmProvisioningResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to set realm active={IsActive} for tenant {TenantId}",
                isActive, tenantId);
            return new PartnerRealmProvisioningResult(false, "Realm update request to identity provider failed");
        }
    }

    public async Task<PartnerRealmProvisioningResult> DeletePartnerRealmAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        EnsureOnboardingEnabled();

        try
        {
            using HttpClient client = _httpClientFactory.CreateClient(HttpClientName);
            string token = await GetAdminTokenAsync(client, cancellationToken);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpResponseMessage response = await client.DeleteAsync(
                $"admin/realms/{Uri.EscapeDataString(tenantId)}",
                cancellationToken);

            if (response.StatusCode is HttpStatusCode.NotFound)
            {
                return new PartnerRealmProvisioningResult(true);
            }

            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Keycloak rejected realm delete for tenant {TenantId}: {Status} {Body}",
                    tenantId, response.StatusCode, body);
                return new PartnerRealmProvisioningResult(false, "Identity provider rejected realm deletion");
            }

            return new PartnerRealmProvisioningResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to delete realm for tenant {TenantId}",
                tenantId);
            return new PartnerRealmProvisioningResult(false, "Realm delete request to identity provider failed");
        }
    }

    private void EnsureOnboardingEnabled()
    {
        if (!_options.PartnerOnboardingEnabled
            || string.IsNullOrWhiteSpace(_options.AdminClientId)
            || string.IsNullOrWhiteSpace(_options.AdminClientSecret))
        {
            throw new InvalidOperationException(
                "Partner onboarding is not configured. Set Keycloak:PartnerOnboardingEnabled, Keycloak:AdminClientId, and Keycloak:AdminClientSecret.");
        }
    }

    private async Task<string> GetAdminTokenAsync(HttpClient client, CancellationToken cancellationToken)
    {
        string adminRealm = !string.IsNullOrWhiteSpace(_options.AdminRealm)
            ? _options.AdminRealm
            : DefaultAdminRealm;

        Dictionary<string, string> form = new()
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _options.AdminClientId!,
            ["client_secret"] = _options.AdminClientSecret!
        };

        using FormUrlEncodedContent content = new(form);
        HttpResponseMessage response = await client.PostAsync(
            $"realms/{Uri.EscapeDataString(adminRealm)}/protocol/openid-connect/token",
            content,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Failed to obtain Keycloak admin token from realm {AdminRealm}: {Status} {Body}",
                adminRealm, response.StatusCode, body);
            throw new InvalidOperationException("Failed to obtain admin token from identity provider.");
        }

        KeycloakTokenResponse? token = await response.Content.ReadFromJsonAsync<KeycloakTokenResponse>(
            cancellationToken);

        if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new InvalidOperationException("Identity provider returned an empty admin token.");
        }

        return token.AccessToken;
    }

    private async Task CreateRealmAsync(
        HttpClient client,
        string tenantId,
        string displayName,
        CancellationToken cancellationToken)
    {
        KeycloakRealmRepresentation payload = new()
        {
            Realm = tenantId,
            DisplayName = displayName,
            Enabled = true
        };

        HttpResponseMessage response = await client.PostAsJsonAsync("admin/realms", payload, cancellationToken);
        await EnsureSuccessAsync(response, $"create realm {tenantId}", cancellationToken);
    }

    private async Task CreateRealmRoleAsync(
        HttpClient client,
        string tenantId,
        string roleName,
        string description,
        CancellationToken cancellationToken)
    {
        KeycloakRoleRepresentation payload = new()
        {
            Name = roleName,
            Description = description
        };

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"admin/realms/{Uri.EscapeDataString(tenantId)}/roles",
            payload,
            cancellationToken);

        if (response.StatusCode is HttpStatusCode.Conflict)
        {
            return;
        }

        await EnsureSuccessAsync(response, $"create role {roleName} in realm {tenantId}", cancellationToken);
    }

    private async Task CreateApiClientAsync(
        HttpClient client,
        string tenantId,
        CancellationToken cancellationToken)
    {
        KeycloakProtocolMapperRepresentation audienceMapper = new()
        {
            Name = "audience-mapper",
            Protocol = "openid-connect",
            ProtocolMapper = "oidc-audience-mapper",
            ConsentRequired = false,
            Config = new Dictionary<string, string>
            {
                ["included.client.audience"] = _options.Audience,
                ["id.token.claim"] = "false",
                ["access.token.claim"] = "true",
                ["introspection.token.claim"] = "true"
            }
        };

        KeycloakClientRepresentation payload = new()
        {
            ClientId = _options.Audience,
            Name = "Wrapsfer API",
            Enabled = true,
            PublicClient = false,
            DirectAccessGrantsEnabled = true,
            StandardFlowEnabled = true,
            DefaultClientScopes = new List<string> { "basic", "profile", "email", "roles" },
            ProtocolMappers = new List<KeycloakProtocolMapperRepresentation> { audienceMapper }
        };

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"admin/realms/{Uri.EscapeDataString(tenantId)}/clients",
            payload,
            cancellationToken);

        await EnsureSuccessAsync(response, $"create client in realm {tenantId}", cancellationToken);
    }

    private async Task<string> CreateAdminUserAsync(
        HttpClient client,
        PartnerRealmProvisioningRequest request,
        CancellationToken cancellationToken)
    {
        KeycloakUserRepresentation payload = new()
        {
            Username = request.AdminUsername,
            Email = request.AdminEmail,
            EmailVerified = true,
            Enabled = true,
            Credentials = new List<KeycloakCredentialRepresentation>
            {
                new()
                {
                    Type = "password",
                    Value = request.TemporaryPassword,
                    Temporary = true
                }
            },
            RequiredActions = new List<string> { "UPDATE_PASSWORD" }
        };

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"admin/realms/{Uri.EscapeDataString(request.TenantId)}/users",
            payload,
            cancellationToken);

        await EnsureSuccessAsync(response, $"create admin user in realm {request.TenantId}", cancellationToken);

        if (response.Headers.Location is not null)
        {
            string path = response.Headers.Location.ToString();
            int idx = path.LastIndexOf('/');
            if (idx >= 0 && idx < path.Length - 1)
            {
                return path[(idx + 1)..];
            }
        }

        return await LookupUserIdByUsernameAsync(client, request.TenantId, request.AdminUsername, cancellationToken);
    }

    private async Task<string> LookupUserIdByUsernameAsync(
        HttpClient client,
        string tenantId,
        string username,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await client.GetAsync(
            $"admin/realms/{Uri.EscapeDataString(tenantId)}/users?exact=true&username={Uri.EscapeDataString(username)}",
            cancellationToken);

        await EnsureSuccessAsync(response, $"lookup user {username} in realm {tenantId}", cancellationToken);

        List<KeycloakUserRepresentation>? users =
            await response.Content.ReadFromJsonAsync<List<KeycloakUserRepresentation>>(
                cancellationToken);

        KeycloakUserRepresentation? user = users?.FirstOrDefault();
        if (user is null || string.IsNullOrWhiteSpace(user.Id))
        {
            throw new InvalidOperationException("Identity provider did not return the newly created user.");
        }

        return user.Id;
    }

    private async Task AssignAdminRoleAsync(
        HttpClient client,
        string tenantId,
        string userId,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage roleResponse = await client.GetAsync(
            $"admin/realms/{Uri.EscapeDataString(tenantId)}/roles/admin",
            cancellationToken);
        await EnsureSuccessAsync(roleResponse, $"read admin role from realm {tenantId}", cancellationToken);

        KeycloakRoleRepresentation? adminRole =
            await roleResponse.Content.ReadFromJsonAsync<KeycloakRoleRepresentation>(
                cancellationToken);

        if (adminRole is null || string.IsNullOrWhiteSpace(adminRole.Id))
        {
            throw new InvalidOperationException("Identity provider did not return the admin role with an id.");
        }

        KeycloakRoleRepresentation[] payload = { adminRole };

        HttpResponseMessage assignResponse = await client.PostAsJsonAsync(
            $"admin/realms/{Uri.EscapeDataString(tenantId)}/users/{Uri.EscapeDataString(userId)}/role-mappings/realm",
            payload,
            cancellationToken);

        await EnsureSuccessAsync(assignResponse, $"assign admin role in realm {tenantId}", cancellationToken);
    }

    private async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogError(
            "Keycloak operation '{Operation}' failed: {Status} {Body}",
            operation, response.StatusCode, Truncate(body, 1024));

        throw new HttpRequestException(
            $"Keycloak admin call failed for '{operation}' with status {(int)response.StatusCode}.");
    }

    private static string Truncate(string value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength];
}
