using Microsoft.Extensions.Logging.Abstractions;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.FileProcessing;
using Wrapsfer.Application.Abstractions.Storage;
using Wrapsfer.Application.Waybills.Commands.EmailWaybillsReport;
using Wrapsfer.Application.Waybills.Commands.RequestWaybillsExport;
using Wrapsfer.Application.Waybills.Options;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;
using Wrapsfer.Mailing.Abstractions;

namespace Wrapsfer.Application.Tests.Waybills.Commands;

public class EmailWaybillsReportCommandHandlerTests
{
    private const string OwnerTenantId = "owner-tenant";
    private const string OwnerUserId = "owner-user";

    private readonly Mock<IWaybillRepository> _waybillRepository = new();
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IPartnerTenantRepository> _partnerTenantRepository = new();
    private readonly Mock<IWaybillReportFileWriter> _writer = new();
    private readonly Mock<IReportStorage> _reportStorage = new();
    private readonly Mock<IMailer> _mailer = new();
    private readonly Mock<ITenantContext> _tenantContext = new();

    private readonly List<MailMessage> _sentMails = [];
    private readonly List<string> _uploadedKeys = [];

    private WaybillEmailReportOptions _options = new();

    public EmailWaybillsReportCommandHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns(OwnerTenantId);
        _tenantContext.Setup(x => x.UserId).Returns(OwnerUserId);

        _writer.Setup(w => w.WriteAsync(
                It.IsAny<IReadOnlyList<WaybillReportRow>>(), It.IsAny<WaybillExportFormat>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 1, 2, 3, 4 });

        _reportStorage.Setup(s => s.UploadAsync(
                It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, byte[] _, string _, CancellationToken _) =>
            {
                _uploadedKeys.Add(key);
                return new StoredReport(
                    "test-bucket", key, $"https://signed.example/{key}", DateTime.UtcNow.AddHours(1));
            });

        _mailer.Setup(m => m.SendAsync(It.IsAny<MailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MailRequestId.New())
            .Callback<MailMessage, CancellationToken>((m, _) => _sentMails.Add(m));
    }

    private EmailWaybillsReportCommandHandler BuildHandler() => new(
        _waybillRepository.Object,
        _productRepository.Object,
        _partnerTenantRepository.Object,
        _writer.Object,
        _reportStorage.Object,
        _mailer.Object,
        _tenantContext.Object,
        Options.Create(_options),
        NullLogger<EmailWaybillsReportCommandHandler>.Instance);

    private void SetupWaybills(string tenantId, int count)
    {
        List<Waybill> waybills = Enumerable.Range(0, count)
            .Select(i => Waybill.Create(tenantId, new DateOnly(2026, 5, 1), $"WB-{tenantId}-{i}").Value)
            .ToList();

        _waybillRepository.Setup(r => r.ListByTenantIdsAsync(
                It.Is<IReadOnlyCollection<string>>(c => c.Contains(tenantId)),
                It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<WaybillStatus?>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<Waybill>)waybills, count));
    }

    private static PartnerTenant Partner(string tenantId) =>
        PartnerTenant.Create(tenantId, tenantId, "owner").Value;

    private static EmailWaybillsReportCommand Command(
        params string[] recipients) =>
        new(WaybillExportFormat.Csv, recipients, null, null, null, null,
            IncludeAllPartnerTenants: false);

    [Fact]
    public async Task Handle_BelowRowThreshold_SendsAttachmentEmail()
    {
        SetupWaybills(OwnerTenantId, 3);

        Result<EmailWaybillsReportResult> result =
            await BuildHandler().Handle(Command("ops@example.com"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.DeliveryMode.Should().Be(WaybillExportDeliveryMode.Attachment);
        result.Value.RowCount.Should().Be(3);

        MailMessage mail = _sentMails.Should().ContainSingle().Which;
        mail.To.Should().BeEquivalentTo("ops@example.com");
        mail.Attachments.Should().ContainSingle();
        mail.Attachments[0].ContentType.Should().Be("text/csv");
        mail.Attachments[0].Content.Should().BeEquivalentTo(new byte[] { 1, 2, 3, 4 });
        mail.Body!.Text.Should().Contain("attached");
        _reportStorage.Verify(s => s.UploadAsync(
                It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_AboveRowThreshold_FallsBackToLink()
    {
        _options = new WaybillEmailReportOptions
        {
            AttachmentRowThreshold = 2,
            AttachmentMaxBytes = 25L * 1024 * 1024
        };
        SetupWaybills(OwnerTenantId, 3);

        Result<EmailWaybillsReportResult> result =
            await BuildHandler().Handle(Command("ops@example.com"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.DeliveryMode.Should().Be(WaybillExportDeliveryMode.Link);
        _uploadedKeys.Should().ContainSingle()
            .Which.Should().StartWith($"waybills/{OwnerTenantId}/");

        MailMessage mail = _sentMails.Should().ContainSingle().Which;
        mail.Attachments.Should().BeEmpty();
        mail.Body!.Text.Should().Contain("https://signed.example/");
        mail.Body.Text.Should().Contain("Link expires (UTC):");
    }

    [Fact]
    public async Task Handle_FileBytesExceedSizeLimit_FallsBackToLinkEvenWithFewRows()
    {
        _options = new WaybillEmailReportOptions
        {
            AttachmentRowThreshold = 1_000,
            AttachmentMaxBytes = 1
        };
        SetupWaybills(OwnerTenantId, 1);

        Result<EmailWaybillsReportResult> result =
            await BuildHandler().Handle(Command("ops@example.com"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.DeliveryMode.Should().Be(WaybillExportDeliveryMode.Link);
        _sentMails.Should().ContainSingle().Which.Attachments.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NonOwnerWithPartnerTenantIds_IsRejected()
    {
        SetupWaybills(OwnerTenantId, 1);

        EmailWaybillsReportCommand command = new(
            WaybillExportFormat.Csv,
            new[] { "ops@example.com" },
            null, null, null, null,
            IncludeAllPartnerTenants: false,
            PartnerTenantIds: new[] { "partner-a" });

        Result<EmailWaybillsReportResult> result =
            await BuildHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(WaybillExportErrors.PartnerScopeNotAllowed);
        _sentMails.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_UnknownPartner_IsRejected()
    {
        _partnerTenantRepository.Setup(r => r.ListAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PartnerTenant> { Partner("partner-a") });

        EmailWaybillsReportCommand command = new(
            WaybillExportFormat.Csv,
            new[] { "ops@example.com" },
            null, null, null, null,
            IncludeAllPartnerTenants: true,
            PartnerTenantIds: new[] { "partner-unknown" });

        Result<EmailWaybillsReportResult> result =
            await BuildHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(WaybillExportErrors.UnknownOrInactivePartner);
        _sentMails.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NoPartnerScope_DefaultsToCurrentTenant()
    {
        SetupWaybills(OwnerTenantId, 1);

        await BuildHandler().Handle(Command("ops@example.com"), CancellationToken.None);

        _waybillRepository.Verify(r => r.ListByTenantIdsAsync(
            It.Is<IReadOnlyCollection<string>>(c => c.Count == 1 && c.Contains(OwnerTenantId)),
            It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<WaybillStatus?>(),
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Handle_SendsToAllRecipients()
    {
        SetupWaybills(OwnerTenantId, 1);

        await BuildHandler().Handle(
            Command("ops@example.com", "billing@example.com"), CancellationToken.None);

        _sentMails.Should().ContainSingle()
            .Which.To.Should().BeEquivalentTo("ops@example.com", "billing@example.com");
    }
}
