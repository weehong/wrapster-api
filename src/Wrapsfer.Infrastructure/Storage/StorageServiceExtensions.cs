using Amazon;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wrapsfer.Application.Abstractions.Storage;

namespace Wrapsfer.Infrastructure.Storage;

public static class StorageServiceExtensions
{
    public static IServiceCollection AddReportStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<ReportStorageOptions>()
            .Bind(configuration.GetSection(ReportStorageOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<ReportStorageOptions>, ReportStorageOptionsValidator>();

        services.AddSingleton<IAmazonS3>(sp =>
        {
            ReportStorageOptions options = sp.GetRequiredService<IOptions<ReportStorageOptions>>().Value;

            // Credentials come from the default AWS SDK provider chain (environment, IAM role, profile).
            return string.IsNullOrWhiteSpace(options.Region)
                ? new AmazonS3Client()
                : new AmazonS3Client(RegionEndpoint.GetBySystemName(options.Region));
        });

        services.AddScoped<IReportStorage, S3ReportStorage>();

        return services;
    }
}
