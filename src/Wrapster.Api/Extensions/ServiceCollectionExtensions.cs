using Asp.Versioning;
using Serilog;
using Wrapster.Application;
using Wrapster.Infrastructure;

namespace Wrapster.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static WebApplicationBuilder AddServices(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, LoggerConfiguration) =>
            LoggerConfiguration.ReadFrom.Configuration(context.Configuration));

        builder.Services.AddControllers();
        builder.Services.AddOpenApi();
        builder.Services.AddProblemDetails();

        builder.Services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);

        string connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new
            InvalidOperationException("Connection string 'DefaultConnection' not configured.");

        builder.Services.AddHealthChecks().AddNpgSql(connectionString);

        return builder;
    }
}
