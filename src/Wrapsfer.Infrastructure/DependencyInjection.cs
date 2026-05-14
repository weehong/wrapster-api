using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.FileProcessing;
using Wrapsfer.Application.Abstractions.IdentityProvisioning;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Repositories;
using Wrapsfer.Infrastructure.Authentication;
using Wrapsfer.Infrastructure.BackgroundServices;
using Wrapsfer.Infrastructure.FileProcessing;
using Wrapsfer.Infrastructure.IdentityProvisioning;
using Wrapsfer.Infrastructure.Persistence;
using Wrapsfer.Infrastructure.Persistence.Interceptors;
using Wrapsfer.Infrastructure.Persistence.Repositories;
using Wrapsfer.Infrastructure.Queue;

namespace Wrapsfer.Infrastructure;

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

        services.AddScoped<IPartnerTenantRepository, PartnerTenantRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductComponentRepository, ProductComponentRepository>();
        services.AddScoped<ITenantSettingsRepository, TenantSettingsRepository>();
        services.AddScoped<IStockAlertLogRepository, StockAlertLogRepository>();
        services.AddScoped<IWaybillRepository, WaybillRepository>();

        services.AddKeycloakAuthentication(configuration);
        services.AddQueueService(configuration);

        services.AddSingleton<CsvProductFileParser>();
        services.AddSingleton<ExcelProductFileParser>();
        services.AddSingleton<IProductFileParser, CompositeProductFileParser>();
        services.AddSingleton<CsvProductFileWriter>();
        services.AddSingleton<ExcelProductFileWriter>();
        services.AddSingleton<IProductFileWriter, CompositeProductFileWriter>();

        services.AddHostedService<ProductsExportConsumer>();
        services.AddHostedService<WaybillsExportConsumer>();
        services.AddHostedService<AutoCancelStaleDraftsJob>();

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

        services.AddHttpClient(KeycloakAdminHttpClient.HttpClientName, (sp, client) =>
        {
            KeycloakOptions opts = sp.GetRequiredService<IOptions<KeycloakOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
        });

        services.AddSingleton<RealmConfigurationCache>();
        services.AddSingleton<IRealmConfigurationCache>(sp => sp.GetRequiredService<RealmConfigurationCache>());
        services.AddSingleton<IIdentityProviderSettings, KeycloakIdentityProviderSettings>();
        services.AddScoped<ITenantRealmResolver, SubdomainTenantRealmResolver>();
        services.AddScoped<MultiTenantJwtBearerEvents>();
        services.AddSingleton<KeycloakAdminHttpClient>();
        services.AddScoped<IIdentityTenantProvisioningService, KeycloakTenantProvisioningService>();
        services.AddScoped<IIdentityAuthService, KeycloakIdentityAuthService>();
        services.AddHostedService<KeycloakIssuerPreflight>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = $"{keycloakOptions.BaseUrl.TrimEnd('/')}/realms/{keycloakOptions.OwnerRealm}";
                options.Audience = keycloakOptions.Audience;
                options.RequireHttpsMetadata = keycloakOptions.RequireHttpsMetadata;
                options.MapInboundClaims = false;

                // Disable default automatic configuration — MultiTenantJwtBearerEvents
                // handles per-tenant OIDC discovery and token validation.
                options.Configuration = new OpenIdConnectConfiguration();

                options.EventsType = typeof(MultiTenantJwtBearerEvents);
            });

        services.AddSingleton<IAuthorizationHandler, OwnerAdminAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, NotPasswordChangeRequiredHandler>();

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.OwnerAdminOnly, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new OwnerAdminRequirement());
                policy.AddRequirements(new NotPasswordChangeRequiredRequirement());
            });

            options.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new NotPasswordChangeRequiredRequirement())
                .Build();
        });

        return services;
    }
}
