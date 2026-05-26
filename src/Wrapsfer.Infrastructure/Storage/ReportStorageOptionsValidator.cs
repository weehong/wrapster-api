using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Wrapsfer.Infrastructure.Storage;

public sealed class ReportStorageOptionsValidator : IValidateOptions<ReportStorageOptions>
{
    private readonly IHostEnvironment _environment;

    public ReportStorageOptionsValidator(IHostEnvironment environment) => _environment = environment;

    public ValidateOptionsResult Validate(string? name, ReportStorageOptions options)
    {
        List<string> failures = new();

        if (options.DownloadUrlTtlMinutes <= 0)
        {
            failures.Add($"{ReportStorageOptions.SectionName}:DownloadUrlTtlMinutes must be greater than zero.");
        }

        // The bucket and region are only mandatory in Production. Locally the report export
        // feature stays dormant until configured, so the host can still boot without S3 set up.
        if (_environment.IsProduction())
        {
            if (string.IsNullOrWhiteSpace(options.BucketName))
            {
                failures.Add($"{ReportStorageOptions.SectionName}:BucketName is required in Production.");
            }

            if (string.IsNullOrWhiteSpace(options.Region))
            {
                failures.Add($"{ReportStorageOptions.SectionName}:Region is required in Production.");
            }
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
