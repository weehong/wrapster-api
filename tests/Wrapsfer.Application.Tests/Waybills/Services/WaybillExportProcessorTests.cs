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
using TenantSettingsEntity = Wrapsfer.Domain.Entities.TenantSettings;

namespace Wrapsfer.Application.Tests.Waybills.Services;

public class WaybillExportProcessorTests
{
    private readonly Mock<IWaybillRepository> _waybillRepository = new();
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IWaybillReportFileWriter> _writer = new();
    private readonly Mock<IReportStorage> _reportStorage = new();
    private readonly Mock<ITenantSettingsRepository> _tenantSettingsRepository = new();
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
            _tenantSettingsRepository.Object,
            _mailer.Object,
            NullLogger<WaybillExportProcessor>.Instance);
    }

    private void SetupTenant(string tenantId, params string[] recipientEmails)
    {
        if (recipientEmails.Length > 0)
        {
            TenantSettingsEntity settings = TenantSettingsEntity.Create(tenantId).Value;
            foreach (string email in recipientEmails)
            {
                settings.AddRecipient(email);
            }

            _tenantSettingsRepository.Setup(r => r.GetByTenantIdAsync(tenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(settings);
        }
        else
        {
            _tenantSettingsRepository.Setup(r => r.GetByTenantIdAsync(tenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((TenantSettingsEntity?)null);
        }

        Waybill waybill = Waybill.Create(tenantId, new DateOnly(2026, 5, 1), $"WB-{tenantId}").Value;
        _waybillRepository.Setup(r => r.ListByTenantIdsAsync(
                It.Is<IReadOnlyCollection<string>>(c => c.Count == 1 && c.Contains(tenantId)),
                It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<WaybillStatus?>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Waybill> { waybill } as IReadOnlyList<Waybill>, 1));
    }

    private static WaybillsExportRequestedMessage Message(params string[] tenantIds) =>
        new(tenantIds, "owner-user", WaybillExportFormat.Csv, new DateTime(2026, 5, 26, 8, 30, 0, DateTimeKind.Utc),
            null, null, null, null);

    [Fact]
    public async Task ProcessAsync_WithMultipleTenants_UploadsOneReportPerTenant()
    {
        SetupTenant("partner-a", "a@example.com");
        SetupTenant("partner-b", "b@example.com");

        await _processor.ProcessAsync(Message("partner-a", "partner-b"), CancellationToken.None);

        _reportStorage.Verify(s => s.UploadAsync(
            It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        _mailer.Verify(m => m.SendAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));

        _uploadedKeys.Should().Contain(k => k.Contains("waybills/partner-a/2026/05/26/"));
        _uploadedKeys.Should().Contain(k => k.Contains("waybills/partner-b/2026/05/26/"));
    }

    [Fact]
    public async Task ProcessAsync_SendsSeparateEmailsPerTenant_WithoutMixingRecipients()
    {
        SetupTenant("partner-a", "a@example.com");
        SetupTenant("partner-b", "b@example.com");

        await _processor.ProcessAsync(Message("partner-a", "partner-b"), CancellationToken.None);

        _sentMails.Should().HaveCount(2);

        MailMessage mailA = _sentMails.Single(m => m.To.Contains("a@example.com"));
        MailMessage mailB = _sentMails.Single(m => m.To.Contains("b@example.com"));

        mailA.To.Should().ContainSingle().Which.Should().Be("a@example.com");
        mailB.To.Should().ContainSingle().Which.Should().Be("b@example.com");
    }

    [Fact]
    public async Task ProcessAsync_TenantWithNoActiveRecipients_IsSkipped_AndOthersContinue()
    {
        SetupTenant("partner-a"); // no recipients configured
        SetupTenant("partner-b", "b@example.com");

        await _processor.ProcessAsync(Message("partner-a", "partner-b"), CancellationToken.None);

        _reportStorage.Verify(s => s.UploadAsync(
            It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _mailer.Verify(m => m.SendAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        _uploadedKeys.Should().OnlyContain(k => k.Contains("waybills/partner-b/"));

        // The skipped tenant's waybills are never even queried.
        _waybillRepository.Verify(r => r.ListByTenantIdsAsync(
            It.Is<IReadOnlyCollection<string>>(c => c.Contains("partner-a")),
            It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<WaybillStatus?>(),
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_EmailContainsDownloadLinkAndNoAttachment()
    {
        SetupTenant("partner-a", "a@example.com");

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
