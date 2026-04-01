using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wrapster.Domain.Abstractions;
using Wrapster.Infrastructure.Persistence;
using Wrapster.Infrastructure.Persistence.Interceptors;

namespace Wrapster.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<AuditableEntityInterceptor>();
        services.AddSingleton<DomainEventInterceptor>();

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            var auditableInterceptor = sp
                .GetRequiredService<AuditableEntityInterceptor>();
            var domainEventInterceptor = sp
                .GetRequiredService<DomainEventInterceptor>();
            string connectionString = configuration.GetConnectionString("DefaultConnection")
                                      ?? throw new InvalidOperationException(
                                          "Connection string 'DefaultConnection' is not configured.");

            options.UseNpgsql(connectionString)
                .AddInterceptors(auditableInterceptor, domainEventInterceptor);
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ApplicationDbContext>());

        return services;
    }
}
