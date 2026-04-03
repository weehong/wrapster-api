using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Wrapster.Application.Abstractions;
using Wrapster.Domain.Abstractions;
using Wrapster.Infrastructure.Authentication;
using Wrapster.Infrastructure.Persistence;
using Wrapster.Infrastructure.Persistence.Interceptors;

namespace Wrapster.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, HttpTenantContext>();

        services.AddScoped<AuditableEntityInterceptor>();
        services.AddScoped<AuditLogInterceptor>();
        services.AddSingleton<DomainEventInterceptor>();

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            AuditableEntityInterceptor auditableInterceptor = sp
                .GetRequiredService<AuditableEntityInterceptor>();
            AuditLogInterceptor auditLogInterceptor = sp
                .GetRequiredService<AuditLogInterceptor>();
            DomainEventInterceptor domainEventInterceptor = sp
                .GetRequiredService<DomainEventInterceptor>();
            string connectionString = configuration.GetConnectionString("DefaultConnection")
                                      ?? throw new InvalidOperationException(
                                          "Connection string 'DefaultConnection' is not configured.");

            options.UseNpgsql(connectionString)
                .AddInterceptors(auditableInterceptor, auditLogInterceptor, domainEventInterceptor);
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.AddKeycloakAuthentication(configuration);

        return services;
    }

    private static IServiceCollection AddKeycloakAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        IConfigurationSection keycloakSection = configuration.GetSection(KeycloakOptions.SectionName);
        services.AddOptions<KeycloakOptions>()
            .Bind(keycloakSection)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<KeycloakOptions>, KeycloakOptionsValidator>();

        KeycloakOptions keycloakOptions = keycloakSection.Get<KeycloakOptions>()
                                          ?? throw new InvalidOperationException(
                                              "Keycloak configuration section is missing.");

        services.AddHttpClient("KeycloakOidc");

        services.AddSingleton<RealmConfigurationCache>();
        services.AddScoped<ITenantRealmResolver, SubdomainTenantRealmResolver>();
        services.AddScoped<MultiTenantJwtBearerEvents>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = $"{keycloakOptions.BaseUrl.TrimEnd('/')}/realms/{keycloakOptions.OwnerRealm}";
                options.Audience = keycloakOptions.Audience;
                options.RequireHttpsMetadata = keycloakOptions.RequireHttpsMetadata;

                // Disable default automatic configuration — MultiTenantJwtBearerEvents
                // handles per-tenant OIDC discovery and token validation.
                options.Configuration = new OpenIdConnectConfiguration();

                options.EventsType = typeof(MultiTenantJwtBearerEvents);
            });

        services.AddAuthorization();

        return services;
    }
}
