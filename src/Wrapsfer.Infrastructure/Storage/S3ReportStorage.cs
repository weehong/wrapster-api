using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Wrapsfer.Application.Abstractions.Storage;

namespace Wrapsfer.Infrastructure.Storage;

internal sealed class S3ReportStorage(
    IAmazonS3 s3Client,
    IOptions<ReportStorageOptions> options) : IReportStorage
{
    private readonly ReportStorageOptions _options = options.Value;

    public async Task<StoredReport> UploadAsync(
        string objectKey,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BucketName))
        {
            throw new InvalidOperationException(
                $"{ReportStorageOptions.SectionName}:BucketName is not configured; cannot upload reports.");
        }

        string fullKey = BuildKey(objectKey);

        using MemoryStream stream = new(content, writable: false);

        // No ACL is set, so the object inherits the bucket's private default — reports are never public.
        PutObjectRequest putRequest = new()
        {
            BucketName = _options.BucketName,
            Key = fullKey,
            InputStream = stream,
            ContentType = contentType,
            AutoCloseStream = false
        };

        await s3Client.PutObjectAsync(putRequest, cancellationToken);

        DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(_options.DownloadUrlTtlMinutes);

        GetPreSignedUrlRequest urlRequest = new()
        {
            BucketName = _options.BucketName,
            Key = fullKey,
            Verb = HttpVerb.GET,
            Expires = expiresAtUtc
        };

        string downloadUrl = await s3Client.GetPreSignedURLAsync(urlRequest);

        return new StoredReport(_options.BucketName, fullKey, downloadUrl, expiresAtUtc);
    }

    private string BuildKey(string objectKey)
    {
        string relativeKey = objectKey.TrimStart('/');
        string prefix = _options.Prefix?.Trim().Trim('/') ?? string.Empty;

        return string.IsNullOrEmpty(prefix)
            ? relativeKey
            : $"{prefix}/{relativeKey}";
    }
}
