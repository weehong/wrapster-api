using Amazon.S3;
using Amazon.S3.Model;
using Wrapsfer.Application.Abstractions.Storage;
using Wrapsfer.Infrastructure.Storage;

namespace Wrapsfer.Application.Tests.Storage;

public class S3ReportStorageTests
{
    private const string Bucket = "wrapsfer-reports";

    private readonly Mock<IAmazonS3> _s3 = new();
    private PutObjectRequest? _putRequest;
    private GetPreSignedUrlRequest? _urlRequest;

    public S3ReportStorageTests()
    {
        _s3.Setup(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse())
            .Callback<PutObjectRequest, CancellationToken>((r, _) => _putRequest = r);

        _s3.Setup(c => c.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
            .ReturnsAsync("https://signed.example/object")
            .Callback<GetPreSignedUrlRequest>(r => _urlRequest = r);
    }

    private S3ReportStorage CreateStorage(string prefix = "reports", int ttlMinutes = 4320) =>
        new(_s3.Object, Options.Create(new ReportStorageOptions
        {
            BucketName = Bucket,
            Region = "ap-southeast-1",
            Prefix = prefix,
            DownloadUrlTtlMinutes = ttlMinutes
        }));

    [Fact]
    public async Task UploadAsync_PrependsConfiguredPrefix_ToFormTenantDatedObjectKey()
    {
        S3ReportStorage storage = CreateStorage();
        string relativeKey = "waybills/partner-a/2026/05/26/abc123/waybills-report-20260526-083000.csv";

        StoredReport result = await storage.UploadAsync(relativeKey, new byte[] { 1 }, "text/csv", CancellationToken.None);

        string expectedKey = $"reports/{relativeKey}";
        _putRequest.Should().NotBeNull();
        _putRequest!.BucketName.Should().Be(Bucket);
        _putRequest.Key.Should().Be(expectedKey);
        _putRequest.ContentType.Should().Be("text/csv");

        result.BucketName.Should().Be(Bucket);
        result.ObjectKey.Should().Be(expectedKey);
        result.DownloadUrl.Should().Be("https://signed.example/object");
    }

    [Fact]
    public async Task UploadAsync_PresignedUrlExpiry_UsesConfiguredTtl()
    {
        S3ReportStorage storage = CreateStorage(ttlMinutes: 4320);
        DateTime expectedExpiry = DateTime.UtcNow.AddMinutes(4320);

        StoredReport result = await storage.UploadAsync(
            "waybills/partner-a/2026/05/26/abc123/report.csv", new byte[] { 1 }, "text/csv", CancellationToken.None);

        _urlRequest.Should().NotBeNull();
        _urlRequest!.Verb.Should().Be(HttpVerb.GET);
        _urlRequest.BucketName.Should().Be(Bucket);
        _urlRequest.Expires.Should().BeCloseTo(expectedExpiry, TimeSpan.FromMinutes(1));
        result.ExpiresAtUtc.Should().BeCloseTo(expectedExpiry, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task UploadAsync_WithEmptyPrefix_UsesRelativeKeyAsIs()
    {
        S3ReportStorage storage = CreateStorage(prefix: "");
        string relativeKey = "waybills/partner-a/2026/05/26/abc123/report.csv";

        await storage.UploadAsync(relativeKey, new byte[] { 1 }, "text/csv", CancellationToken.None);

        _putRequest!.Key.Should().Be(relativeKey);
    }
}
