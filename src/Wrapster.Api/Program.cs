using Serilog;
using Wrapster.Api.Extensions;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Wrapster API builder...");

    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.AddServices();

    WebApplication app = builder.Build();

    app.UseSerilogRequestLogging();
    app.ConfigurePipeline();

    Log.Information("Starting Wrapster API in {Environment} environment", app.Environment.EnvironmentName);

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

// Required for integration testing (WebApplicationFactory)
public partial class Program
{
}
