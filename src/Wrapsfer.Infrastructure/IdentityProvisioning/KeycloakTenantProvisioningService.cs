using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wrapsfer.Application.Abstractions.IdentityProvisioning;
using Wrapsfer.Infrastructure.Authentication;
using Wrapsfer.Infrastructure.IdentityProvisioning.Dtos;

namespace Wrapsfer.Infrastructure.IdentityProvisioning;

internal sealed class KeycloakTenantProvisioningService : IIdentityTenantProvisioningService
{
    internal const string RequiresPasswordChangeAttribute = "requires_password_change";

    private readonly KeycloakAdminHttpClient _adminHttp;
    private readonly ILogger<KeycloakTenantProvisioningService> _logger;
    private readonly KeycloakOptions _options;

    public KeycloakTenantProvisioningService(
        KeycloakAdminHttpClient adminHttp,
        IOptions<KeycloakOptions> options,
        ILogger<KeycloakTenantProvisioningService> logger)
    {
        _adminHttp = adminHttp;
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
            using HttpClient client = await _adminHttp.CreateAuthorizedClientAsync(cancellationToken);

            await CreateRealmAsync(client, request.TenantId, request.DisplayName, cancellationToken);

            // The admin token's `resource_access` is fixed at issuance time and does not include
            // the newly-created realm's management client, so subsequent calls to that realm's
            // admin endpoints return 403. Refresh the token to pick up the new resource_access entry.
            await _adminHttp.RefreshAuthorizationAsync(client, cancellationToken);

            await EnableUnmanagedAttributesAsync(client, request.TenantId, cancellationToken);
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
            using HttpClient client = await _adminHttp.CreateAuthorizedClientAsync(cancellationToken);

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
            using HttpClient client = await _adminHttp.CreateAuthorizedClientAsync(cancellationToken);

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
            || string.IsNullOrWhiteSpace(_options.AdminClientSecret)
            || string.IsNullOrWhiteSpace(_options.PartnerApiClientSecret))
        {
            throw new InvalidOperationException(
                "Partner onboarding is not configured. Set Keycloak:PartnerOnboardingEnabled, " +
                "Keycloak:AdminClientId, Keycloak:AdminClientSecret, and Keycloak:PartnerApiClientSecret.");
        }
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
        await _adminHttp.EnsureSuccessAsync(response, $"create realm {tenantId}", cancellationToken);
    }

    private async Task EnableUnmanagedAttributesAsync(
        HttpClient client,
        string tenantId,
        CancellationToken cancellationToken)
    {
        // Keycloak 26's user profile (a) silently drops any attribute not declared in the realm's
        // user profile config and (b) marks `firstName` / `lastName` as required for users by default.
        // Onboarding doesn't capture either name, so the partner admin would be rejected by Keycloak
        // ("Account is not fully set up") on every login. Switch the realm to `ADMIN_EDIT` so the API
        // can set `requires_password_change`, and drop the required-flag on the name fields.
        HttpResponseMessage getResponse = await client.GetAsync(
            $"admin/realms/{Uri.EscapeDataString(tenantId)}/users/profile",
            cancellationToken);

        await _adminHttp.EnsureSuccessAsync(getResponse, $"read user profile for realm {tenantId}", cancellationToken);

        string body = await getResponse.Content.ReadAsStringAsync(cancellationToken);
        System.Text.Json.Nodes.JsonNode? profile = System.Text.Json.Nodes.JsonNode.Parse(body);

        if (profile is null)
        {
            throw new InvalidOperationException(
                $"Identity provider returned an empty user profile config for realm {tenantId}.");
        }

        profile["unmanagedAttributePolicy"] = "ADMIN_EDIT";

        if (profile["attributes"] is System.Text.Json.Nodes.JsonArray attributes)
        {
            foreach (System.Text.Json.Nodes.JsonNode? attribute in attributes)
            {
                if (attribute is null)
                {
                    continue;
                }

                string? name = attribute["name"]?.GetValue<string>();
                if (name is "firstName" or "lastName")
                {
                    attribute.AsObject().Remove("required");
                }
            }
        }

        HttpResponseMessage putResponse = await client.PutAsJsonAsync(
            $"admin/realms/{Uri.EscapeDataString(tenantId)}/users/profile",
            profile,
            cancellationToken);

        await _adminHttp.EnsureSuccessAsync(putResponse, $"enable unmanaged attributes for realm {tenantId}", cancellationToken);
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

        await _adminHttp.EnsureSuccessAsync(response, $"create role {roleName} in realm {tenantId}", cancellationToken);
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

        KeycloakProtocolMapperRepresentation requiresPasswordChangeMapper = new()
        {
            Name = "requires-password-change-mapper",
            Protocol = "openid-connect",
            ProtocolMapper = "oidc-usermodel-attribute-mapper",
            ConsentRequired = false,
            Config = new Dictionary<string, string>
            {
                ["user.attribute"] = RequiresPasswordChangeAttribute,
                ["claim.name"] = RequiresPasswordChangeAttribute,
                ["jsonType.label"] = "boolean",
                ["access.token.claim"] = "true",
                ["id.token.claim"] = "false",
                ["userinfo.token.claim"] = "false",
                ["introspection.token.claim"] = "true",
                ["multivalued"] = "false",
                ["aggregate.attrs"] = "false"
            }
        };

        KeycloakClientRepresentation payload = new()
        {
            ClientId = _options.Audience,
            Name = "Wrapsfer API",
            Enabled = true,
            PublicClient = false,
            Secret = _options.PartnerApiClientSecret,
            DirectAccessGrantsEnabled = true,
            StandardFlowEnabled = true,
            DefaultClientScopes = new List<string> { "basic", "profile", "email", "roles" },
            ProtocolMappers = new List<KeycloakProtocolMapperRepresentation>
            {
                audienceMapper,
                requiresPasswordChangeMapper
            }
        };

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"admin/realms/{Uri.EscapeDataString(tenantId)}/clients",
            payload,
            cancellationToken);

        await _adminHttp.EnsureSuccessAsync(response, $"create client in realm {tenantId}", cancellationToken);
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
                    Temporary = false
                }
            }
        };

        if (request.IsTemporaryPassword)
        {
            payload.Attributes = new Dictionary<string, List<string>>
            {
                [RequiresPasswordChangeAttribute] = new() { "true" }
            };
        }

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"admin/realms/{Uri.EscapeDataString(request.TenantId)}/users",
            payload,
            cancellationToken);

        await _adminHttp.EnsureSuccessAsync(response, $"create admin user in realm {request.TenantId}", cancellationToken);

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

        await _adminHttp.EnsureSuccessAsync(response, $"lookup user {username} in realm {tenantId}", cancellationToken);

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
        await _adminHttp.EnsureSuccessAsync(roleResponse, $"read admin role from realm {tenantId}", cancellationToken);

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

        await _adminHttp.EnsureSuccessAsync(assignResponse, $"assign admin role in realm {tenantId}", cancellationToken);
    }
}
