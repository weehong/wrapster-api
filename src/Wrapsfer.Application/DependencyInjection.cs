using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Wrapsfer.Application.Abstractions.Auditing;
using Wrapsfer.Application.Behaviors;
using Wrapsfer.Application.Billing;
using Wrapsfer.Application.Products.Services;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Application.Waybills.Services;

namespace Wrapsfer.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        Assembly assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(assembly);
            // AuditBehavior is registered first so it wraps validation: a request that fails
            // validation (returned as Result.Failure by ValidationBehavior) is still audited.
            config.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>));
            config.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            config.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);

        services.AddSingleton<AuditMetadataAccumulator>();
        services.AddSingleton<IAuditMetadata>(sp => sp.GetRequiredService<AuditMetadataAccumulator>());

        services.AddScoped<StockReservationService>();
        services.AddScoped<LowStockAlertService>();
        services.AddScoped<LowStockReminderProcessor>();
        services.AddScoped<WaybillExportProcessor>();
        services.AddScoped<ShopeeTokenRefreshProcessor>();

        services.AddSingleton<StockReportProrationCalculator>();
        services.AddScoped<StockReportBillingStatusFactory>();
        services.AddScoped<StripeCustomerProvisioner>();

        return services;
    }
}
