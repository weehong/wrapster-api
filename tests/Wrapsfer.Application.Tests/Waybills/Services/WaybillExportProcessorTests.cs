using Microsoft.Extensions.Logging.Abstractions;
using Wrapsfer.Application.Abstractions.FileProcessing;
using Wrapsfer.Application.Abstractions.Storage;
using Wrapsfer.Application.Waybills.Commands.RequestWaybillsExport;
using Wrapsfer.Application.Waybills.Messaging;
using Wrapsfer.Application.Waybills.Services;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;
using Wrapsfer.Mailing.Abstractions;

namespace Wrapsfer.Application.Tests.Waybills.Services;

public class WaybillExportProcessorTests
{
    private readonly Mock<IWaybillRepository> _waybillRepository = new();
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IWaybillReportFileWriter> _writer = new();
    private readonly Mock<IReportStorage> _reportStorage = new();
    private readonly Mock<IMailer> _mailer = new();

    private readonly List<MailMessage> _sentMails = [];
    private readonly List<string> _uploadedKeys = [];

    private readonly WaybillExportProcessor _processor;

    public WaybillExportProcessorTests()
    {
        _writer.Setup(w => w.WriteAsync(
                It.IsAny<IReadOnlyList<WaybillReportRow>>(), It.IsAny<WaybillExportFormat>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 1, 2, 3 });

        _reportStorage.Setup(s => s.UploadAsync(
                It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, byte[] _, string _, CancellationToken _) =>
            {
                _uploadedKeys.Add(key);
                return new StoredReport("test-bucket", key, $"https://signed.example/{key}",
                    DateTime.UtcNow.AddMinutes(4320));
            });

        _mailer.Setup(m => m.SendAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MailRequestId.New())
            .Callback<MailMessage, CancellationToken>((m, _) => _sentMails.Add(m));

        _processor = new WaybillExportProcessor(
            _waybillRepository.Object,
            _productRepository.Object,
            _writer.Object,
            _reportStorage.Object,
            _mailer.Object,
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
        new(Guid.Empty, tenantIds, "owner-user", WaybillExportFormat.Csv,
            new DateTime(2026, 5, 26, 8, 30, 0, DateTimeKind.Utc),
            null, null, null, null);

    [Fact]
    public async Task ProcessAsync_WithMultipleTenants_UploadsOneReportPerTenant()
    {
        SetupTenant("partner-a");
        SetupTenant("partner-b");

        await _processor.ProcessAsync(Message("partner-a", "partner-b"), CancellationToken.None);

        _reportStorage.Verify(s => s.UploadAsync(
            It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        _mailer.Verify(m => m.SendAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));

        _uploadedKeys.Should().Contain(k => k.Contains("waybills/partner-a/2026/05/26/"));
        _uploadedKeys.Should().Contain(k => k.Contains("waybills/partner-b/2026/05/26/"));
    }

    [Fact]
    public async Task ProcessAsync_SendsEachPartnersReportToItsTaggedMailbox()
    {
        SetupTenant("partner-a");
        SetupTenant("partner-b");

        await _processor.ProcessAsync(Message("partner-a", "partner-b"), CancellationToken.None);

        _sentMails.Should().HaveCount(2);

        // TEMPORARY: every export is routed to weehongkane+{tenantId}@gmail.com,
        // tagged per partner, regardless of configured recipients.
        MailMessage mailA = _sentMails.Single(m => m.To.Contains("weehongkane+partner-a@gmail.com"));
        MailMessage mailB = _sentMails.Single(m => m.To.Contains("weehongkane+partner-b@gmail.com"));

        mailA.To.Should().ContainSingle().Which.Should().Be("weehongkane+partner-a@gmail.com");
        mailB.To.Should().ContainSingle().Which.Should().Be("weehongkane+partner-b@gmail.com");
    }

    [Fact]
    public async Task ProcessAsync_EmailContainsDownloadLinkAndNoAttachment()
    {
        SetupTenant("partner-a");

        await _processor.ProcessAsync(Message("partner-a"), CancellationToken.None);

        MailMessage mail = _sentMails.Should().ContainSingle().Which;
        mail.Attachments.Should().BeEmpty();
        mail.Body.Should().NotBeNull();
        mail.Body!.Html.Should().BeNull();
        mail.Body.Text.Should().Contain("Download:");
        mail.Body.Text.Should().Contain("https://signed.example/");
        mail.Body.Text.Should().Contain("Link expires (UTC):");
    }
}
