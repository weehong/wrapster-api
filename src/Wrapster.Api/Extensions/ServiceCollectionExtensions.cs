using System.Text.Json.Serialization;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using RabbitMQ.Client;
using Serilog;
using Serilog.Extensions.Logging;
using Wrapster.Api.Filters;
using Wrapster.Application;
using Wrapster.Application.Products;
using Wrapster.Infrastructure;
using Wrapster.Infrastructure.Configuration;
using Wrapster.Mailing;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Wrapster.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static WebApplicationBuilder AddServices(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, LoggerConfiguration) =>
            LoggerConfiguration.ReadFrom.Configuration(context.Configuration));

        using SerilogLoggerFactory loggerFactory = new(Log.Logger);
        ILogger startupLogger =
            loggerFactory.CreateLogger(nameof(InfisicalSecretProvider));

        builder.Services.AddInfisicalSecrets(
            builder.Configuration,
            startupLogger,
            builder.Environment.EnvironmentName);

        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(
                    new JsonStringEnumConverter());
            })
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    Dictionary<string, string[]> errors = context.ModelState
                        .Where(e => e.Value?.Errors.Count > 0)
                        .ToDictionary(
                            e => e.Key,
                            e => e.Value!.Errors.Select(err => err.ErrorMessage).ToArray());

                    // Remove the misleading top-level parameter name error (e.g. "command")
                    // when the real issue is a JSON deserialization failure on a specific field
                    if (errors.Count > 1)
                    {
                        bool hasJsonError = errors.Values
                            .SelectMany(v => v)
                            .Any(msg => msg.Contains("could not be converted"));

                        if (hasJsonError)
                        {
                            errors = errors
                                .Where(e => !e.Value.Any(msg => msg.Contains("field is required")))
                                .ToDictionary(e => e.Key, e => e.Value);
                        }
                    }

                    ProblemDetails problemDetails = new()
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "Validation Error",
                        Detail = "One or more fields in the request body are invalid."
                    };
                    problemDetails.Extensions["errors"] = errors;
                    problemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

                    return new BadRequestObjectResult(problemDetails);
                };
            });
        builder.Services.AddScoped<TenantResolutionFilter>();
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

        builder.Services.Configure<ProductSettings>(
            builder.Configuration.GetSection(ProductSettings.SectionName));

        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddMailing(builder.Configuration);

        string connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new
            InvalidOperationException("Connection string 'DefaultConnection' not configured.");

        ConnectionFactory rabbitConnectionFactory = new()
        {
            HostName = builder.Configuration["RabbitMq:HostName"]
                       ?? throw new InvalidOperationException("RabbitMq:HostName is not configured."),
            Port = int.TryParse(builder.Configuration["RabbitMq:Port"], out int rabbitPort)
                ? rabbitPort
                : throw new InvalidOperationException("RabbitMq:Port is not configured or invalid."),
            UserName = builder.Configuration["RabbitMq:UserName"]
                       ?? throw new InvalidOperationException("RabbitMq:UserName is not configured."),
            Password = builder.Configuration["RabbitMq:Password"]
                       ?? throw new InvalidOperationException("RabbitMq:Password is not configured."),
            VirtualHost = builder.Configuration["RabbitMq:VirtualHost"] ?? "/"
        };

        builder.Services.AddHealthChecks()
            .AddNpgSql(connectionString)
            .AddRabbitMQ(sp => rabbitConnectionFactory.CreateConnectionAsync());

        return builder;
    }
}
