using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wrapsfer.Infrastructure.Authentication;
using Wrapsfer.Infrastructure.IdentityProvisioning.Dtos;

namespace Wrapsfer.Infrastructure.IdentityProvisioning;

internal sealed class KeycloakAdminHttpClient
{
    internal const string HttpClientName = "KeycloakAdmin";
    private const string DefaultAdminRealm = "master";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly KeycloakOptions _options;
    private readonly ILogger<KeycloakAdminHttpClient> _logger;

    public KeycloakAdminHttpClient(
        IHttpClientFactory httpClientFactory,
        IOptions<KeycloakOptions> options,
        ILogger<KeycloakAdminHttpClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public HttpClient CreateClient() => _httpClientFactory.CreateClient(HttpClientName);

    public async Task<HttpClient> CreateAuthorizedClientAsync(CancellationToken cancellationToken)
    {
        HttpClient client = CreateClient();
        await RefreshAuthorizationAsync(client, cancellationToken);
        return client;
    }

    public async Task RefreshAuthorizationAsync(HttpClient client, CancellationToken cancellationToken)
    {
        string token = await GetAdminTokenAsync(client, cancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<string> GetAdminTokenAsync(HttpClient client, CancellationToken cancellationToken)
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

    public async Task EnsureSuccessAsync(
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
