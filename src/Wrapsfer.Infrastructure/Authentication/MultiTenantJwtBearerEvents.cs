using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Infrastructure.Authentication;

public sealed class MultiTenantJwtBearerEvents : JwtBearerEvents
{
    private readonly ILogger<MultiTenantJwtBearerEvents> _logger;
    private readonly KeycloakOptions _options;
    private readonly RealmConfigurationCache _realmConfigurationCache;
    private readonly ITenantRealmResolver _realmResolver;

    public MultiTenantJwtBearerEvents(
        ITenantRealmResolver realmResolver,
        RealmConfigurationCache realmConfigurationCache,
        IOptions<KeycloakOptions> options,
        ILogger<MultiTenantJwtBearerEvents> logger)
    {
        _realmResolver = realmResolver;
        _realmConfigurationCache = realmConfigurationCache;
        _options = options.Value;
        _logger = logger;
    }

    public override async Task MessageReceived(MessageReceivedContext context)
    {
        string realm = _realmResolver.ResolveRealm(context.Request);

        _logger.LogDebug("Resolved tenant realm: {Realm}", realm);

        context.HttpContext.Items[KeycloakClaimParser.TenantRealmKey] = realm;

        string? token = context.Token;

        if (string.IsNullOrEmpty(token))
        {
            string? authorization = context.Request.Headers.Authorization.ToString();

            if (!string.IsNullOrEmpty(authorization) &&
                authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = authorization["Bearer ".Length..].Trim();
            }
        }

        if (string.IsNullOrEmpty(token))
        {
            // No token present — let the default handler produce a 401.
            await base.MessageReceived(context);
            return;
        }

        OpenIdConnectConfiguration oidcConfig = await _realmConfigurationCache.GetConfigurationAsync(
            realm, context.HttpContext.RequestAborted);

        TokenValidationParameters tvp = context.Options.TokenValidationParameters.Clone();
        tvp.ValidIssuer = $"{_options.BaseUrl.TrimEnd('/')}/realms/{realm}";
        tvp.IssuerSigningKeys = oidcConfig.SigningKeys;

        try
        {
            JwtSecurityTokenHandler handler = new() { MapInboundClaims = false };
            ClaimsPrincipal principal = handler.ValidateToken(token, tvp, out _);

            if (!string.Equals(realm, _options.OwnerRealm, StringComparison.OrdinalIgnoreCase))
            {
                bool partnerAllowed = await IsPartnerActiveAsync(context, realm);
                if (!partnerAllowed)
                {
                    _logger.LogInformation(
                        "Token rejected: partner tenant {Realm} is inactive or unknown",
                        realm);
                    context.Fail("Tenant is inactive or unknown.");
                    return;
                }
            }

            context.Principal = principal;
            context.Success();
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogInformation(ex, "JWT validation failed for realm: {Realm}", realm);
            context.Fail(ex);
        }
    }

    private async Task<bool> IsPartnerActiveAsync(MessageReceivedContext context, string realm)
    {
        IPartnerTenantRepository repository =
            context.HttpContext.RequestServices.GetRequiredService<IPartnerTenantRepository>();

        PartnerTenant? partner = await repository.GetByTenantIdAsync(realm, context.HttpContext.RequestAborted);

        if (partner is null)
        {
            return false;
        }

        return partner.IsActive && partner.ProvisioningStatus == PartnerTenantProvisioningStatus.Active;
    }

    public override async Task AuthenticationFailed(AuthenticationFailedContext context)
    {
        string realm = context.HttpContext.Items[KeycloakClaimParser.TenantRealmKey] as string ?? "unknown";

        _logger.LogInformation(context.Exception, "JWT Authentication failed for realm: {Realm}", realm);

        await base.AuthenticationFailed(context);
    }
}
