using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Auditing;
using Wrapsfer.Application.Abstractions.FileProcessing;
using Wrapsfer.Application.Abstractions.IdentityProvisioning;
using Wrapsfer.Application.PartnerIntegrations.Options;
using Wrapsfer.Application.Waybills.Options;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Repositories;
using Wrapsfer.Infrastructure.Authentication;
using Wrapsfer.Infrastructure.BackgroundServices;
using Wrapsfer.Infrastructure.Billing;
using Wrapsfer.Infrastructure.FileProcessing;
using Wrapsfer.Infrastructure.IdentityProvisioning;
using Wrapsfer.Infrastructure.Persistence;
using Wrapsfer.Infrastructure.Persistence.Auditing;
using Wrapsfer.Infrastructure.Persistence.Interceptors;
using Wrapsfer.Infrastructure.Persistence.Repositories;
using Wrapsfer.Infrastructure.Queue;
using Wrapsfer.Infrastructure.Storage;

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
        services.AddScoped<StockMovementInterceptor>();
        services.AddSingleton<DomainEventInterceptor>();

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            AuditableEntityInterceptor auditableInterceptor = sp
                .GetRequiredService<AuditableEntityInterceptor>();
            AuditLogInterceptor auditLogInterceptor = sp
                .GetRequiredService<AuditLogInterceptor>();
            StockMovementInterceptor stockMovementInterceptor = sp
                .GetRequiredService<StockMovementInterceptor>();
            DomainEventInterceptor domainEventInterceptor = sp
                .GetRequiredService<DomainEventInterceptor>();
            string connectionString = configuration.GetConnectionString("DefaultConnection")
                                      ?? throw new InvalidOperationException(
                                          "Connection string 'DefaultConnection' is not configured.");

            options.UseNpgsql(connectionString)
                .AddInterceptors(auditableInterceptor, auditLogInterceptor, stockMovementInterceptor,
                    domainEventInterceptor);
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.AddSingleton<IAuditSink, AuditSink>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();

        services.AddScoped<IPartnerTenantRepository, PartnerTenantRepository>();
        services.AddScoped<IPartnerIntegrationCredentialRepository, PartnerIntegrationCredentialRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductComponentRepository, ProductComponentRepository>();
        services.AddScoped<IStockMovementRepository, StockMovementRepository>();
        services.AddScoped<ITenantSettingsRepository, TenantSettingsRepository>();
        services.AddScoped<IStockAlertLogRepository, StockAlertLogRepository>();
        services.AddScoped<IWaybillRepository, WaybillRepository>();
        services.AddScoped<IWaybillExportJobRepository, WaybillExportJobRepository>();
        services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
        services.AddScoped<IPartnerBillingCustomerRepository, PartnerBillingCustomerRepository>();
        services.AddScoped<IFeatureEntitlementRepository, FeatureEntitlementRepository>();
        services.AddScoped<IStripeWebhookEventRepository, StripeWebhookEventRepository>();

        services.AddKeycloakAuthentication(configuration);
        services.AddQueueService(configuration);
        services.AddReportStorage(configuration);
        services.AddStripeBilling(configuration);

        services.AddOptions<WaybillEmailReportOptions>()
            .Bind(configuration.GetSection(WaybillEmailReportOptions.SectionName));

        services.AddOptions<PartnerIntegrationOptions>()
            .Bind(configuration.GetSection(PartnerIntegrationOptions.SectionName));

        services.AddSingleton<CsvProductFileParser>();
        services.AddSingleton<ExcelProductFileParser>();
        services.AddSingleton<IProductFileParser, CompositeProductFileParser>();
        services.AddSingleton<CsvProductFileWriter>();
        services.AddSingleton<ExcelProductFileWriter>();
        services.AddSingleton<IProductFileWriter, CompositeProductFileWriter>();
        services.AddSingleton<CsvWaybillReportFileWriter>();
        services.AddSingleton<ExcelWaybillReportFileWriter>();
        services.AddSingleton<PdfWaybillReportFileWriter>();
        services.AddSingleton<IWaybillReportFileWriter, CompositeWaybillReportFileWriter>();
        services.AddSingleton<ExcelProductStockReportFileWriter>();
        services.AddSingleton<PdfProductStockReportFileWriter>();
        services.AddSingleton<IProductStockReportFileWriter, CompositeProductStockReportFileWriter>();

        services.AddHostedService<OutboxProcessor>();
        services.AddHostedService<DomainEventConsumer>();
        services.AddHostedService<ProductsExportConsumer>();
        services.AddHostedService<WaybillsExportConsumer>();
        services.AddHostedService<AutoCancelStaleDraftsJob>();
        services.AddHostedService<LowStockReminderJob>();
        services.AddHostedService<StockMovementBackfillJob>();

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
        services.AddScoped<IIntegrationClientProvisioningService, KeycloakIntegrationClientProvisioningService>();
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
        services.AddSingleton<IAuthorizationHandler, PartnerIntegrationAdminAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, IntegrationApiAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, NotIntegrationClientHandler>();
        services.AddSingleton<IAuthorizationHandler, NotPasswordChangeRequiredHandler>();

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.OwnerAdminOnly, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new OwnerAdminRequirement());
                policy.AddRequirements(new NotPasswordChangeRequiredRequirement());
                policy.AddRequirements(new NotIntegrationClientRequirement());
            });

            options.AddPolicy(AuthorizationPolicies.PartnerIntegrationAdmin, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PartnerIntegrationAdminRequirement());
                policy.AddRequirements(new NotPasswordChangeRequiredRequirement());
                policy.AddRequirements(new NotIntegrationClientRequirement());
            });

            options.AddPolicy(AuthorizationPolicies.IntegrationApiOnly, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new IntegrationApiRequirement());
                policy.AddRequirements(new NotPasswordChangeRequiredRequirement());
            });

            // Integration (M2M) tokens are denied on every endpoint by default; the only
            // surface open to them is endpoints guarded by the IntegrationApiOnly policy.
            options.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new NotPasswordChangeRequiredRequirement())
                .AddRequirements(new NotIntegrationClientRequirement())
                .Build();
        });

        return services;
    }
}
