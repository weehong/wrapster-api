using Microsoft.Extensions.Logging.Abstractions;
using Wrapsfer.Application.Abstractions.FileProcessing;
using Wrapsfer.Application.Abstractions.Storage;
using Wrapsfer.Application.Waybills.Commands.RequestWaybillsExport;
using Wrapsfer.Application.Waybills.Messaging;
using Wrapsfer.Application.Waybills.Services;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Waybills.Services;

public class WaybillExportProcessorTests
{
    private readonly Mock<IWaybillRepository> _waybillRepository = new();
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IWaybillReportFileWriter> _writer = new();
    private readonly Mock<IReportStorage> _reportStorage = new();

    private readonly List<string> _uploadedKeys = [];

    private readonly WaybillExportProcessor _processor;

    public WaybillExportProcessorTests()
    {
        _writer.Setup(w => w.WriteAsync(
                It.IsAny<IReadOnlyList<WaybillReportRow>>(), It.IsAny<WaybillReportMetadata>(),
                It.IsAny<WaybillExportFormat>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 1, 2, 3 });

        _reportStorage.Setup(s => s.UploadAsync(
                It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, byte[] _, string _, CancellationToken _) =>
            {
                _uploadedKeys.Add(key);
                return new StoredReport("test-bucket", key, $"https://signed.example/{key}",
                    DateTime.UtcNow.AddMinutes(4320));
            });

        _processor = new WaybillExportProcessor(
            _waybillRepository.Object,
            _productRepository.Object,
            _writer.Object,
            _reportStorage.Object,
            NullLogger<WaybillExportProcessor>.Instance);
    }

    private void SetupTenant(string tenantId)
    {
        Waybill waybill = Waybill.Create(tenantId, new DateOnly(2026, 5, 1), $"WB-{tenantId}").Value;
        _waybillRepository.Setup(r => r.ListByTenantIdsAsync(
                It.Is<IReadOnlyCollection<string>>(c => c.Count == 1 && c.Contains(tenantId)),
                It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<WaybillStatus?>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Waybill> { waybill } as IReadOnlyList<Waybill>, 1));
    }

    private static WaybillsExportRequestedMessage Message(params string[] tenantIds) =>
        new(Guid.Empty, tenantIds, "owner-user", "Owner User", WaybillExportFormat.Csv,
            new DateTime(2026, 5, 26, 8, 30, 0, DateTimeKind.Utc),
            null, null, null, null);

    [Fact]
    public async Task ProcessAsync_WithMultipleTenants_UploadsOneReportPerTenant()
    {
        SetupTenant("partner-a");
        SetupTenant("partner-b");

        IReadOnlyList<string> keys = await _processor.ProcessAsync(
            Message("partner-a", "partner-b"), CancellationToken.None);

        _reportStorage.Verify(s => s.UploadAsync(
            It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        keys.Should().HaveCount(2);
        _uploadedKeys.Should().Contain(k => k.StartsWith("waybills/partner-a/2026-05-26/"));
        _uploadedKeys.Should().Contain(k => k.StartsWith("waybills/partner-b/2026-05-26/"));
    }

    [Fact]
    public async Task ProcessAsync_ReturnsObjectKeyForEachUploadedTenant()
    {
        SetupTenant("partner-a");
        SetupTenant("partner-b");

        IReadOnlyList<string> keys = await _processor.ProcessAsync(
            Message("partner-a", "partner-b"), CancellationToken.None);

        keys.Should().HaveCount(2);
        keys.Should().Contain(k => k.StartsWith("waybills/partner-a/2026-05-26/"));
        keys.Should().Contain(k => k.StartsWith("waybills/partner-b/2026-05-26/"));
    }

    [Fact]
    public async Task ProcessAsync_UploadHasExpectedContentTypeAndExtension()
    {
        SetupTenant("partner-a");

        WaybillsExportRequestedMessage pdfMessage = new(
            Guid.Empty, ["partner-a"], "owner-user", "Owner User", WaybillExportFormat.Pdf,
            new DateTime(2026, 5, 26, 8, 30, 0, DateTimeKind.Utc),
            null, null, null, null);

        await _processor.ProcessAsync(pdfMessage, CancellationToken.None);

        _reportStorage.Verify(s => s.UploadAsync(
            It.Is<string>(k => k.EndsWith(".pdf")),
            It.IsAny<byte[]>(),
            "application/pdf",
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
