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

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BucketName))
        {
            throw new InvalidOperationException(
                $"{ReportStorageOptions.SectionName}:BucketName is not configured; cannot delete reports.");
        }

        // UploadAsync returns StoredReport.ObjectKey already prefixed, and that's the value the
        // caller persists and passes back here — do NOT re-apply BuildKey or we'd end up trying
        // to delete "reports/reports/...".
        DeleteObjectRequest request = new()
        {
            BucketName = _options.BucketName,
            Key = objectKey
        };

        // S3's DeleteObject is idempotent — a missing key responds 204 No Content rather than 404,
        // so we don't need a HEAD probe or a try/catch for NotFound.
        await s3Client.DeleteObjectAsync(request, cancellationToken);
    }

    public async Task<byte[]?> DownloadAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BucketName))
        {
            throw new InvalidOperationException(
                $"{ReportStorageOptions.SectionName}:BucketName is not configured; cannot download reports.");
        }

        try
        {
            GetObjectRequest request = new()
            {
                BucketName = _options.BucketName,
                Key = objectKey
            };

            using GetObjectResponse response = await s3Client.GetObjectAsync(request, cancellationToken);
            using MemoryStream buffer = new();
            await response.ResponseStream.CopyToAsync(buffer, cancellationToken);
            return buffer.ToArray();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
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
